using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.App.ViewModels;
using MouseSim.Core.Hotkeys;
using MouseSim.Core.Input;
using MouseSim.Core.Persistence;

namespace MouseSim.App.Services;

/// <summary>组合根：创建所有服务并完成接线（热键 → 界面动作）。</summary>
public sealed class AppServices : IDisposable
{
    public IInputSimulator Simulator { get; } = new SendInputSimulator();
    public GlobalHotkeyManager Hotkeys { get; } = new();
    public AppDataStore Store { get; } = new();
    public StatusViewModel Status { get; } = new();
    public RunnerService Runner { get; }
    public RecorderService Recorder { get; }
    public FloatingStatusService Floating { get; }
    public MainViewModel Main { get; }

    private AppSettings _settings = new();
    private int _startId = -1;
    private int _stopId = -1;
    private int _recordId = -1;

    public AppServices()
    {
        Runner = new RunnerService(Simulator, Status);
        // 录制时要排除热键按键，避免 F6/F8/F9 被录进脚本
        Recorder = new RecorderService(Status, () =>
        [
            _settings.StartHotkey.VirtualKey,
            _settings.StopHotkey.VirtualKey,
            _settings.RecordHotkey.VirtualKey,
        ]);
        Floating = new FloatingStatusService(Runner, Recorder, Status);

        var clicker = new ClickerViewModel(Runner, Status);
        var scripts = new ScriptViewModel(Store, Runner, Status);
        var settings = new SettingsViewModel(Store);
        Main = new MainViewModel(clicker, scripts, settings, Status);
        Main.AttachRecorder(new RecorderViewModel(Recorder, scripts, Main));

        // 点击悬浮卡 = 停止当前动作：录制中走 VM 停止流程（会展示录制结果），否则停止任务
        Floating.StopRequested += () => Ui.OnUi(() =>
        {
            if (Recorder.IsRecording) Main.ToggleRecording();
            else if (Runner.IsRunning) Runner.Stop();
        });
    }

    public async void Initialize()
    {
        try
        {
            _settings = await Store.LoadSettingsAsync();
            Main.Settings.LoadFrom(_settings);
            Main.Settings.HotkeysChanged += () => _ = ApplyHotkeysAsync();
            Hotkeys.HotkeyPressed += OnHotkeyPressed;

            // 停止录制 / 手动停止任务后，把主窗口带回前台方便查看结果
            Recorder.RecordingChanged += () =>
            {
                if (!Recorder.IsRecording) Ui.ShowMainWindow();
            };
            Runner.TaskUserStopped += Ui.ShowMainWindow;

            // 录制新热键期间临时注销全部热键（否则按键会被旧热键吞掉），退出后恢复
            HotkeyCaptureArbiter.CaptureStarted += () => _ = UnregisterAllHotkeysAsync();
            HotkeyCaptureArbiter.CaptureEnded += () => _ = ApplyHotkeysAsync();

            await ApplyHotkeysAsync();
            Main.Scripts.Load();
        }
        catch (Exception ex)
        {
            Log.Error("初始化", ex);
            Status.SetText("初始化失败：" + ex.Message);
        }
    }

    private void OnHotkeyPressed(int id, HotkeyDefinition _)
    {
        Ui.OnUi(() =>
        {
            if (id == _startId) Main.StartCurrent();
            else if (id == _stopId) Runner.Stop();
            else if (id == _recordId) Main.ToggleRecording();
        });
    }

    private async Task ApplyHotkeysAsync()
    {
        await UnregisterAllHotkeysAsync();

        // 各处按钮文案跟随当前配置的热键名
        Status.StartHotkeyText = Main.Settings.StartHotkey.Display;
        Status.StopHotkeyText = Main.Settings.StopHotkey.Display;
        Status.RecordHotkeyText = Main.Settings.RecordHotkey.Display;

        var failures = new List<string>();
        _startId = await TryRegister(Main.Settings.StartHotkey, "开始", failures);
        _stopId = await TryRegister(Main.Settings.StopHotkey, "停止", failures);
        _recordId = await TryRegister(Main.Settings.RecordHotkey, "录制", failures);

        if (failures.Count == 0)
        {
            Status.HotkeyHint = $"{Main.Settings.StartHotkey} 开始 · {Main.Settings.StopHotkey} 停止 · {Main.Settings.RecordHotkey} 录制";
        }
        else
        {
            Status.HotkeyHint = "部分热键未生效";
            Status.SetText("热键注册失败：" + string.Join("；", failures));
        }
    }

    private async Task<int> TryRegister(HotkeyDefinition hotkey, string purpose, List<string> failures)
    {
        try
        {
            return await Hotkeys.RegisterAsync(hotkey);
        }
        catch (Exception ex)
        {
            failures.Add($"【{purpose}】{hotkey} → {ex.Message}");
            return -1;
        }
    }

    private async Task UnregisterAllHotkeysAsync()
    {
        if (_startId >= 0) { await Hotkeys.UnregisterAsync(_startId); _startId = -1; }
        if (_stopId >= 0) { await Hotkeys.UnregisterAsync(_stopId); _stopId = -1; }
        if (_recordId >= 0) { await Hotkeys.UnregisterAsync(_recordId); _recordId = -1; }
    }

    public void Dispose()
    {
        Floating.Dispose();
        Hotkeys.Dispose();
    }
}
