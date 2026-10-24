using System.IO;
using System.Windows;
using MouseSim.App.Services;
using MouseSim.App.Views;
using MouseSim.Core.Persistence;

namespace MouseSim.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("界面未处理异常", args.Exception);
            MessageBox.Show(args.Exception.Message, "MouseSim 出错", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("未处理异常", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Log.Error("未观察的任务异常", args.Exception);

        Services = new AppServices();
        Services.Initialize();

        var window = new MainWindow { DataContext = Services.Main };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.Dispose();
        base.OnExit(e);
    }
}

/// <summary>简单的错误日志（%APPDATA%\MouseSim\logs\error.log），用于排查问题。</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static void Error(string context, Exception? exception)
    {
        try
        {
            var dir = Path.Combine(AppDataStore.AppDataDir, "logs");
            Directory.CreateDirectory(dir);
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n{exception}\n\n");
            }
        }
        catch
        {
            // 日志失败不影响程序运行
        }
    }
}
