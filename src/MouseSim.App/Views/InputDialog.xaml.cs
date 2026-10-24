using System.Windows;

namespace MouseSim.App.Views;

public partial class InputDialog : Window
{
    private InputDialog(string title, string prompt, string initial)
    {
        InitializeComponent();
        Title = title;
        Prompt.Text = prompt;
        Input.Text = initial;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>显示输入框。用户确认返回输入内容（已去空白），取消返回 null。</summary>
    public static string? Show(string title, string prompt, string initial = "")
    {
        var dialog = new InputDialog(title, prompt, initial) { Owner = Application.Current?.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Input.Text.Trim() : null;
    }
}
