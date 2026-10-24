using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MouseSim.App.Views;

public partial class ScriptView : UserControl
{
    public ScriptView()
    {
        InitializeComponent();
    }

    // 左键点击“添加”直接弹出类型菜单（ContextMenu 默认只在右键时弹出）
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
