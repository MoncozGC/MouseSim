using System.Collections.Concurrent;
using MouseSim.Core.Input;

namespace MouseSim.Core.Hotkeys;

/// <summary>
/// 全局热键管理器。内部跑一条专用线程：
/// RegisterHotKey 与消息循环都必须在创建它们的线程上执行，
/// 注册/注销请求通过 PostThreadMessage 投递到该线程处理。
/// 注意：HotkeyPressed 事件在管理器线程上触发，UI 侧需自行调度。
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const uint WmRegisterRequest = Win32.WM_APP + 1;
    private const uint WmUnregisterRequest = Win32.WM_APP + 2;

    private enum RequestKind : byte { Register, Unregister }

    private sealed record Request(RequestKind Kind, HotkeyDefinition? Hotkey, int Id, TaskCompletionSource<int> Tcs);

    private readonly ConcurrentQueue<Request> _queue = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private readonly Dictionary<int, HotkeyDefinition> _registered = [];
    private uint _threadId;
    private int _nextId;
    private volatile bool _disposed;

    /// <summary>热键被按下时触发（管理器线程）。</summary>
    public event Action<int, HotkeyDefinition>? HotkeyPressed;

    public GlobalHotkeyManager()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "MouseSim.Hotkeys" };
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("热键管理线程启动失败");
    }

    /// <summary>注册全局热键，返回热键 id。失败时抛出异常（通常是与其他软件冲突）。</summary>
    public Task<int> RegisterAsync(HotkeyDefinition hotkey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new Request(RequestKind.Register, hotkey, 0, tcs));
        Post(WmRegisterRequest);
        return tcs.Task;
    }

    public Task UnregisterAsync(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new Request(RequestKind.Unregister, null, id, tcs));
        Post(WmUnregisterRequest);
        return tcs.Task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 取消所有请求的等待，WM_QUIT 会让 GetMessage 返回 0，线程退出前会注销全部热键
        while (_queue.TryDequeue(out var r))
            r.Tcs.TrySetCanceled();

        Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(3));
        _ready.Dispose();
    }

    private void Run()
    {
        // PeekMessage 强制创建线程消息队列，确保后续 PostThreadMessage 不丢失
        Win32.PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        _threadId = Win32.GetCurrentThreadId();
        _ready.Set();

        while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            switch (msg.Message)
            {
                case Win32.WM_HOTKEY:
                    if (_registered.TryGetValue((int)msg.WParam, out var hk))
                        TryFire((int)msg.WParam, hk);
                    break;
                case WmRegisterRequest:
                case WmUnregisterRequest:
                    DrainQueue();
                    break;
            }
        }

        foreach (var id in _registered.Keys)
            Win32.UnregisterHotKey(IntPtr.Zero, id);
        _registered.Clear();
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var r))
        {
            try
            {
                if (r.Kind == RequestKind.Register)
                {
                    var id = ++_nextId;
                    uint mods = (uint)r.Hotkey!.Modifiers | Win32.MOD_NOREPEAT;
                    if (Win32.RegisterHotKey(IntPtr.Zero, id, mods, (uint)r.Hotkey.VirtualKey))
                    {
                        _registered[id] = r.Hotkey;
                        r.Tcs.SetResult(id);
                    }
                    else
                    {
                        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        r.Tcs.SetException(new InvalidOperationException(
                            $"热键【{r.Hotkey}】注册失败（错误码 {err}），可能已被其他程序占用，请换个组合键。"));
                    }
                }
                else
                {
                    Win32.UnregisterHotKey(IntPtr.Zero, r.Id);
                    _registered.Remove(r.Id);
                    r.Tcs.SetResult(r.Id);
                }
            }
            catch (Exception ex)
            {
                r.Tcs.TrySetException(ex);
            }
        }
    }

    private void TryFire(int id, HotkeyDefinition hk)
    {
        try
        {
            HotkeyPressed?.Invoke(id, hk);
        }
        catch
        {
            // 回调异常不应影响消息循环
        }
    }

    private void Post(uint message)
    {
        if (!Win32.PostThreadMessage(_threadId, message, IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException($"无法与热键线程通信（消息 0x{message:X}）");
    }
}
