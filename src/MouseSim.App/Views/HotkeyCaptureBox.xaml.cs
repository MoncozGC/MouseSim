using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.Core.Hotkeys;
using MouseSim.Core.Input;

namespace MouseSim.App.Views;

/// <summary>热键录制控件：点击获得焦点后，按下组合键即完成录制，Esc 取消。</summary>
public partial class HotkeyCaptureBox : UserControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(HotkeyDefinition), typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((HotkeyCaptureBox)d).UpdateDisplay()));

    private bool _capturing;

    public HotkeyCaptureBox()
    {
        InitializeComponent();
        UpdateDisplay();
        LostKeyboardFocus += (_, _) => StopCapture(clearBorderOnly: true);
    }

    public HotkeyDefinition? Hotkey
    {
        get => (HotkeyDefinition?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        Focus();
        Keyboard.Focus(this);
        _capturing = true;
        HotkeyCaptureArbiter.NotifyStarted(); // 先临时注销全局热键，避免按键被旧热键吞掉
        UpdateDisplay();
        Root.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        Root.Background = (System.Windows.Media.Brush)FindResource("AccentLightBrush");
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_capturing) return;

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            StopCapture();
            return;
        }

        // 只按了修饰键时等待后续按键
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
        {
            return;
        }

        var modifiers = KeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= KeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= KeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= KeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= KeyModifiers.Win;

        Hotkey = new HotkeyDefinition
        {
            Modifiers = modifiers,
            VirtualKey = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key),
        };
        StopCapture();
    }

    private void StopCapture(bool clearBorderOnly = false)
    {
        if (!_capturing && clearBorderOnly) return;
        var wasCapturing = _capturing;
        _capturing = false;
        if (wasCapturing) HotkeyCaptureArbiter.NotifyEnded(); // 恢复全局热键
        Root.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrushSoft");
        Root.Background = System.Windows.Media.Brushes.White;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (Display == null) return;
        Display.Text = _capturing ? "请按下组合键（Esc 取消）" : Hotkey?.Display ?? "（未设置）";
        Display.Foreground = _capturing
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
    }
}
