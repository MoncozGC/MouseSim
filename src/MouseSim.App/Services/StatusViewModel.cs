using System.Windows;
using MouseSim.App.Infrastructure;

namespace MouseSim.App.Services;

/// <summary>底部状态栏的共享状态，任何线程都可安全更新。</summary>
public sealed class StatusViewModel : ObservableObject
{
    private string _text = "就绪";
    private bool _isRunning;
    private bool _isRecording;
    private string _hotkeyHint = "";
    private string _startHotkeyText = "F6";
    private string _stopHotkeyText = "F8";
    private string _recordHotkeyText = "F9";

    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public bool IsRunning { get => _isRunning; set => SetProperty(ref _isRunning, value); }
    public bool IsRecording { get => _isRecording; set => SetProperty(ref _isRecording, value); }
    public string HotkeyHint { get => _hotkeyHint; set => SetProperty(ref _hotkeyHint, value); }

    /// <summary>当前生效的热键显示名（页面按钮文案跟随它们变化）。</summary>
    public string StartHotkeyText { get => _startHotkeyText; set => SetProperty(ref _startHotkeyText, value); }
    public string StopHotkeyText { get => _stopHotkeyText; set => SetProperty(ref _stopHotkeyText, value); }
    public string RecordHotkeyText { get => _recordHotkeyText; set => SetProperty(ref _recordHotkeyText, value); }

    public void SetText(string text) => Ui.OnUi(() => Text = text);
    public void SetRunning(string text) => Ui.OnUi(() => { Text = text; IsRunning = true; });
    public void SetIdle(string text) => Ui.OnUi(() => { Text = text; IsRunning = false; });
    public void SetRecording(bool recording, string text) => Ui.OnUi(() => { Text = text; IsRecording = recording; });
}
