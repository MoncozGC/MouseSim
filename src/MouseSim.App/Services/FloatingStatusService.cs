using System.Diagnostics;
using System.Windows.Threading;
using MouseSim.App.Infrastructure;
using MouseSim.App.Views;

namespace MouseSim.App.Services;

/// <summary>
/// 桌面右下角悬浮状态卡：录制与任务运行期间置顶显示进度，
/// 让人在其他程序里操作时也能一眼看到 MouseSim 的工作状态。
/// </summary>
public sealed class FloatingStatusService : IDisposable
{
    private readonly RunnerService _runner;
    private readonly RecorderService _recorder;
    private readonly StatusViewModel _status;
    private readonly Stopwatch _elapsed = new();
    private FloatingStatusWindow? _window;
    private DispatcherTimer? _timer;
    private string _title = "";
    private string _detail = "";

    public FloatingStatusService(RunnerService runner, RecorderService recorder, StatusViewModel status)
    {
        _runner = runner;
        _recorder = recorder;
        _status = status;

        _runner.RunningChanged += OnRunningChanged;
        _runner.ScriptProgress += (loop, index, _) => UpdateDetail($"第 {loop + 1} 轮 · 第 {index + 1} 步（{_status.StopHotkeyText} 停止）");
        _runner.ClickerProgress += count => UpdateDetail($"已点击 {count} 次（{_status.StopHotkeyText} 停止）");
        _runner.BatchStage += (loop, index, script) =>
            UpdateDetail($"第 {loop + 1} 轮 · 第 {index + 1} 个脚本：{script.Name}（{_status.StopHotkeyText} 停止）");

        _recorder.RecordingChanged += OnRecordingChanged;
        _recorder.RecordedCountChanged += count => UpdateDetail($"已录 {count} 个动作（{_status.RecordHotkeyText} 停止）");
    }

    // —— 任务运行（脚本 / 批量 / 连点）——

    private void OnRunningChanged(object? sender, EventArgs e)
    {
        if (_runner.IsRunning)
        {
            _elapsed.Restart();
            var duration = _runner.CurrentDuration;
            Ui.OnUi(() =>
            {
                Show();
                SetTitle(_runner.CurrentLabel ?? "正在运行");
                if (duration is { } total)
                {
                    _detail = $"已 00:00 · 剩 {Format(total)}（{_status.StopHotkeyText} 停止）";
                    EnsureTimer();
                }
                else
                {
                    StopTimer();
                    _detail = $"准备中…（{_status.StopHotkeyText} 停止）";
                }
                ApplyDetail();
            });
        }
        else
        {
            _elapsed.Stop();
            Ui.OnUi(() =>
            {
                StopTimer();
                _window?.Hide();
            });
        }
    }

    // —— 录制 ——

    private void OnRecordingChanged()
    {
        if (_recorder.IsRecording)
        {
            Ui.OnUi(() =>
            {
                StopTimer();
                Show();
                SetTitle("● 正在录制");
                _detail = $"去目标程序正常操作（{_status.RecordHotkeyText} 停止）";
                ApplyDetail();
            });
        }
        else
        {
            Ui.OnUi(() => _window?.Hide());
        }
    }

    /// <summary>用户点击悬浮卡请求停止当前动作（由 AppServices 路由到录制/任务）。</summary>
    public event Action? StopRequested;

    // —— 更新与展示 ——

    private void UpdateDetail(string detail)
    {
        // 按时长的运行由每秒计时器负责刷新，进度事件不覆盖
        if (_runner.IsRunning && _runner.CurrentDuration is not null) return;

        _detail = detail;
        Ui.OnUi(ApplyDetail);
    }

    private void Show()
    {
        if (_window == null)
        {
            _window = new FloatingStatusWindow();
            _window.StopRequested += () => StopRequested?.Invoke();
        }
        _window.ShowAtBottomRight();
    }

    private void SetTitle(string title)
    {
        _title = title;
        _window?.SetContent(_title, _detail);
    }

    private void ApplyDetail() => _window?.SetContent(_title, _detail);

    private void EnsureTimer()
    {
        _timer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick -= OnTimerTick;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer() => _timer?.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_runner.IsRunning || _runner.CurrentDuration is not { } total)
        {
            StopTimer();
            return;
        }

        var elapsed = _elapsed.Elapsed;
        var remaining = total - elapsed;
        _detail = remaining > TimeSpan.Zero
            ? $"已 {Format(elapsed)} · 剩 {Format(remaining)}（{_status.StopHotkeyText} 停止）"
            : $"已 {Format(elapsed)} · 即将结束（{_status.StopHotkeyText} 停止）";
        ApplyDetail();
    }

    private static string Format(TimeSpan t)
    {
        t = t < TimeSpan.Zero ? TimeSpan.Zero : t;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    public void Dispose()
    {
        _runner.RunningChanged -= OnRunningChanged;
        _recorder.RecordingChanged -= OnRecordingChanged;
        StopTimer();
        Ui.OnUi(() => _window?.Close());
    }
}
