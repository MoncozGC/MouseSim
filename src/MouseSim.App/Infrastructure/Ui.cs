using System.Windows;

namespace MouseSim.App.Infrastructure;

/// <summary>把操作调度回 UI 线程。</summary>
internal static class Ui
{
    public static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    /// <summary>把主窗口带到前台（恢复最小化、绕过前台限制）。</summary>
    public static void ShowMainWindow()
    {
        OnUi(() =>
        {
            var window = Application.Current?.MainWindow;
            if (window == null) return;

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            if (!window.IsVisible)
                window.Show();
            window.Activate();

            // 系统前台限制的兜底：短暂置顶再还原
            if (!window.IsActive)
            {
                window.Topmost = true;
                window.Topmost = false;
            }
            window.Focus();
        });
    }
}
