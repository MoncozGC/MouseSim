using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MouseSim.App.Views;

/// <summary>
/// 桌面右下角的置顶状态悬浮卡：
/// - WS_EX_NOACTIVATE：点击不抢焦点，不打断正在操作的程序（鼠标点击仍然有效）
/// - WS_EX_TOOLWINDOW：不出现在任务栏和 Alt+Tab
/// - 点击整卡 = 停止当前的录制/任务（StopRequested 事件）
/// </summary>
public partial class FloatingStatusWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>用户点击卡片请求停止当前动作。</summary>
    public event Action? StopRequested;

    public FloatingStatusWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        };
    }

    public void SetContent(string title, string detail)
    {
        TitleText.Text = title;
        DetailText.Text = detail;
    }

    public void ShowAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 14;
        if (!IsVisible) Show();
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => StopRequested?.Invoke();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
