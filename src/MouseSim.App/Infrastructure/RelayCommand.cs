using System.Windows.Input;

namespace MouseSim.App.Infrastructure;

/// <summary>无参数命令。CanExecute 变化依赖 WPF CommandManager 自动刷新。</summary>
public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>带字符串参数的命令。</summary>
public class RelayCommand<T>(Action<T> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
        => canExecute?.Invoke(parameter is T t ? t : default) ?? true;

    public void Execute(object? parameter) => _execute(parameter is T t ? t : throw new ArgumentException($"参数类型应为 {typeof(T).Name}", nameof(parameter)));
}
