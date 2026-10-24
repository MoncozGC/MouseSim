using MouseSim.App.Infrastructure;
using MouseSim.App.Services;

namespace MouseSim.App.ViewModels;

public class MainViewModel : ObservableObject
{
    /// <summary>标签页索引常量：0 录制，1 重复，2 连点器，3 设置。</summary>
    public const int RecorderTabIndex = 0;
    public const int ScriptsTabIndex = 1;
    public const int ClickerTabIndex = 2;

    private int _activeTab;

    public MainViewModel(ClickerViewModel clicker, ScriptViewModel scripts, SettingsViewModel settings, StatusViewModel status)
    {
        Clicker = clicker;
        Scripts = scripts;
        Settings = settings;
        Status = status;
    }

    /// <summary>录制页 VM 需要引用本对象（保存后跳转），存在构造循环，
    /// 由 AppServices 在两侧都建好后附加。</summary>
    public RecorderViewModel? Recorder { get; private set; }

    public void AttachRecorder(RecorderViewModel recorder) => Recorder = recorder;

    public int ActiveTab
    {
        get => _activeTab;
        set => SetProperty(ref _activeTab, value);
    }

    public ClickerViewModel Clicker { get; }
    public ScriptViewModel Scripts { get; }
    public SettingsViewModel Settings { get; }
    public StatusViewModel Status { get; }

    /// <summary>开始热键：对当前标签页执行对应的开始操作。</summary>
    public void StartCurrent()
    {
        if (Recorder?.IsRecording == true)
        {
            Status.SetText("正在录制，请先按录制热键停止录制");
            return;
        }

        switch (ActiveTab)
        {
            case ScriptsTabIndex:
                Scripts.RunSelected();
                break;
            case ClickerTabIndex:
                Clicker.Start();
                break;
            default:
                Status.SetText("请切到「重复」或「连点器」页后再按开始热键");
                break;
        }
    }

    /// <summary>录制热键。</summary>
    public void ToggleRecording() => Recorder?.ToggleRecording();
}
