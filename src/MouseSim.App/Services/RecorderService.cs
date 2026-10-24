using System.Diagnostics;
using MouseSim.App.Infrastructure;
using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Recording;

namespace MouseSim.App.Services;

/// <summary>
/// 录制服务：管理钩子安装与录制器生命周期，
/// 录制期间忽略对本程序窗口的鼠标操作与热键按键。
/// </summary>
public sealed class RecorderService
{
    private readonly GlobalInputHook _hook = new();
    private readonly StatusViewModel _status;
    private readonly Func<int[]> _hotkeyVks;
    private readonly object _gate = new();
    private MacroRecorder? _recorder;
    private bool _installed;
    private long _lastUiRefresh;

    public RecorderService(StatusViewModel status, Func<int[]> hotkeyVks)
    {
        _status = status;
        _hotkeyVks = hotkeyVks;
    }

    public bool IsRecording
    {
        get { lock (_gate) return _recorder != null; }
    }

    public event Action? RecordingChanged;

    /// <summary>录制中的动作计数变化（节流约 200ms），供悬浮状态卡显示。</summary>
    public event Action<int>? RecordedCountChanged;

    public async Task StartAsync()
    {
        lock (_gate)
        {
            if (_recorder != null) return;
        }

        if (!_installed)
        {
            await _hook.InstallAsync();
            _installed = true;
        }

        var hotkeys = _hotkeyVks();
        var recorder = new MacroRecorder(
            ignoreMouse: e => ScreenInfo.IsOwnProcessWindowAt(e.X, e.Y),
            ignoreKey: e => hotkeys.Contains(e.VirtualKey));

        lock (_gate)
        {
            if (_recorder != null) return; // 并发启动
            _recorder = recorder;
        }

        _hook.Mouse += recorder.OnMouse;
        _hook.Keyboard += recorder.OnKey;
        _hook.Mouse += OnMouseActivity;
        _hook.Keyboard += OnKeyActivity;

        _status.SetRecording(true, "正在录制（F9 停止）");
        RecordingChanged?.Invoke();
    }

    /// <summary>停止录制并返回动作序列。</summary>
    public async Task<List<IAction>> StopAsync()
    {
        MacroRecorder? recorder;
        lock (_gate)
        {
            recorder = _recorder;
            _recorder = null;
        }

        if (recorder == null) return [];

        _hook.Mouse -= recorder.OnMouse;
        _hook.Keyboard -= recorder.OnKey;
        _hook.Mouse -= OnMouseActivity;
        _hook.Keyboard -= OnKeyActivity;

        var actions = recorder.Stop();
        await _hook.UninstallAsync();
        _installed = false; // 钩子已卸载，下次录制必须重新安装

        _status.SetRecording(false, $"录制完成，共 {actions.Count} 个动作");
        RecordingChanged?.Invoke();
        return actions;
    }

    // 实时状态刷新（节流：钩子事件很频繁，尤其鼠标移动）
    private void OnMouseActivity(MouseHookEvent _) => RefreshLiveStatus();

    private void OnKeyActivity(KeyHookEvent _) => RefreshLiveStatus();

    private void RefreshLiveStatus()
    {
        var now = Stopwatch.GetTimestamp();
        if (now - _lastUiRefresh < TimeSpan.FromMilliseconds(200).Ticks) return;
        _lastUiRefresh = now;

        var recorder = _recorder;
        if (recorder == null) return;
        _status.SetText($"正在录制：{recorder.RecordedCount} 个动作 · 最后：{recorder.LastDescription}");
        RecordedCountChanged?.Invoke(recorder.RecordedCount);
    }
}
