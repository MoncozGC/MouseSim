using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MouseSim.Core.Input;

namespace MouseSim.Core.Recording;

/// <summary>
/// 全局输入钩子：低级鼠标/键盘钩子（WH_MOUSE_LL / WH_KEYBOARD_LL）。
/// 钩子必须安装在有消息循环的线程上，因此与 GlobalHotkeyManager 相同的模式：
/// 专用线程 + PostThreadMessage 投递安装/卸载请求。
/// 事件在钩子线程上触发，订阅方自行处理线程调度；回调异常不影响钩子链。
/// </summary>
public sealed class GlobalInputHook : IDisposable
{
    private const uint WmInstall = Win32.WM_APP + 21;
    private const uint WmUninstall = Win32.WM_APP + 22;
    private const uint WmQuit = Win32.WM_APP + 23;

    private readonly object _gate = new();
    private readonly Win32.HookProc _mouseProc;
    private readonly Win32.HookProc _keyProc;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ConcurrentQueue<(bool Install, TaskCompletionSource<bool> Tcs)> _queue = new();
    private uint _threadId;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private volatile bool _disposed;

    /// <summary>原始鼠标事件（钩子线程触发，含移动）。</summary>
    public event Action<MouseHookEvent>? Mouse;

    /// <summary>原始键盘事件（钩子线程触发）。</summary>
    public event Action<KeyHookEvent>? Keyboard;

    /// <summary>钩子回调内部异常（诊断用）。正常情况不应触发。</summary>
    public event Action<Exception>? CallbackFault;

    /// <summary>鼠标钩子回调被调用的次数（诊断用，含被过滤/异常的情况）。</summary>
    public long MouseProcCalls => _mouseProcCalls;

    private long _mouseProcCalls;

    public GlobalInputHook()
    {
        _mouseProc = OnMouseHook;
        _keyProc = OnKeyHook;
        _thread = new Thread(Run) { IsBackground = true, Name = "MouseSim.InputHook" };
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("输入钩子线程启动失败");
    }

    public bool IsInstalled
    {
        get
        {
            lock (_gate) return _mouseHook != IntPtr.Zero;
        }
    }

    public Task InstallAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return RunOnHookThread(install: true);
    }

    public Task UninstallAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return RunOnHookThread(install: false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_queue.TryDequeue(out var r))
            r.Tcs.TrySetCanceled();

        Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(3));
        _ready.Dispose();
    }

    private Task RunOnHookThread(bool install)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((install, tcs));
        if (!Win32.PostThreadMessage(_threadId, install ? WmInstall : WmUninstall, IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException("无法与输入钩子线程通信");
        return tcs.Task;
    }

    private void Run()
    {
        Win32.PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // 强制创建消息队列
        _threadId = Win32.GetCurrentThreadId();
        _ready.Set();

        while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.Message is WmInstall or WmUninstall)
                DrainQueue();
        }

        RemoveHooks();
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var r))
        {
            try
            {
                if (r.Install)
                {
                    if (IsInstalled)
                    {
                        r.Tcs.SetResult(true);
                        continue;
                    }

                    var module = Win32.GetModuleHandle(null);
                    lock (_gate)
                    {
                        _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, module, 0);
                        _keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyProc, module, 0);
                    }

                    if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
                    {
                        int err = Marshal.GetLastWin32Error();
                        RemoveHooks();
                        r.Tcs.SetException(new InvalidOperationException(
                            $"安装输入钩子失败（错误码 {err}）。部分安全软件会拦截全局监听，请允许 MouseSim 运行。"));
                    }
                    else
                    {
                        r.Tcs.SetResult(true);
                    }
                }
                else
                {
                    RemoveHooks();
                    r.Tcs.SetResult(true);
                }
            }
            catch (Exception ex)
            {
                r.Tcs.TrySetException(ex);
            }
        }
    }

    private void RemoveHooks()
    {
        lock (_gate)
        {
            if (_mouseHook != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_keyboardHook != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }
    }

    private IntPtr OnMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        Interlocked.Increment(ref _mouseProcCalls);
        if (nCode >= 0 && Mouse != null)
        {
            try
            {
                var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                var msg = (uint)wParam.ToInt64();
                var (type, delta) = msg switch
                {
                    Win32.WM_MOUSEMOVE => (MouseHookEventType.Move, 0),
                    Win32.WM_LBUTTONDOWN => (MouseHookEventType.LeftDown, 0),
                    Win32.WM_LBUTTONUP => (MouseHookEventType.LeftUp, 0),
                    Win32.WM_RBUTTONDOWN => (MouseHookEventType.RightDown, 0),
                    Win32.WM_RBUTTONUP => (MouseHookEventType.RightUp, 0),
                    Win32.WM_MBUTTONDOWN => (MouseHookEventType.MiddleDown, 0),
                    Win32.WM_MBUTTONUP => (MouseHookEventType.MiddleUp, 0),
                    Win32.WM_MOUSEWHEEL => (MouseHookEventType.Scroll, HighWord(data.MouseData)),
                    Win32.WM_MOUSEHWHEEL => (MouseHookEventType.ScrollHorizontal, HighWord(data.MouseData)),
                    _ => ((MouseHookEventType?)null, 0),
                };

                if (type != null)
                {
                    Mouse(new MouseHookEvent(
                        type.Value,
                        data.Pt.X,
                        data.Pt.Y,
                        delta,
                        data.Time,
                        (data.Flags & Win32.LLMHF_INJECTED) != 0,
                        data.ExtraInfo));
                }
            }
            catch (Exception ex)
            {
                TryReportFault(ex);
            }
        }

        return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr OnKeyHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Keyboard != null)
        {
            try
            {
                var data = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                var msg = (uint)wParam.ToInt64();
                var isDown = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
                if (isDown || msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP)
                {
                    Keyboard(new KeyHookEvent(
                        isDown,
                        (int)data.VkCode,
                        data.Time,
                        (data.Flags & Win32.LLKHF_INJECTED) != 0,
                        data.ExtraInfo));
                }
            }
            catch (Exception ex)
            {
                TryReportFault(ex);
            }
        }

        return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void TryReportFault(Exception ex)
    {
        try
        {
            CallbackFault?.Invoke(ex);
        }
        catch
        {
            // 诊断事件本身失败则忽略
        }
    }

    private static int HighWord(uint value) => unchecked((short)((value >> 16) & 0xFFFF));
}
