using System.Windows;
using MouseSim.App.ViewModels;
using MouseSim.Core.Actions;

namespace MouseSim.App.Views;

public partial class ActionEditWindow : Window
{
    private readonly ActionEditViewModel _viewModel;

    private ActionEditWindow(ActionEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // 构建失败说明校验未通过，保持对话框打开并显示错误
        if (_viewModel.BuildAction() == null) return;
        DialogResult = true;
    }

    /// <summary>编辑一个动作。确认返回 true，并通过 result 返回新动作；取消返回 false。</summary>
    public static bool Show(IAction action, out IAction result)
    {
        var viewModel = new ActionEditViewModel(action);
        var window = new ActionEditWindow(viewModel) { Owner = Application.Current?.MainWindow };
        if (window.ShowDialog() != true)
        {
            result = action;
            return false;
        }

        result = viewModel.BuildAction()!;
        return true;
    }
}
