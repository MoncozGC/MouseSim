using MouseSim.Core.Input;

namespace MouseSim.Core.Actions;

/// <summary>移动鼠标到指定坐标。</summary>
public class MouseMoveAction : IAction
{
    public int X { get; set; }
    public int Y { get; set; }

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.MoveTo(X, Y);
        return Task.CompletedTask;
    }

    public string Describe() => $"移动鼠标到 ({X}, {Y})";
}

/// <summary>在指定位置（或当前位置）单击/双击鼠标。</summary>
public class MouseClickAction : IAction
{
    public MouseButton Button { get; set; } = MouseButton.Left;
    public bool UseCurrentPosition { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool DoubleClick { get; set; }

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!UseCurrentPosition) simulator.MoveTo(X, Y);
        if (DoubleClick) simulator.DoubleClick(Button);
        else simulator.Click(Button);
        return Task.CompletedTask;
    }

    public string Describe()
    {
        var where = UseCurrentPosition ? "当前位置" : $"({X}, {Y})";
        var button = Button switch
        {
            MouseButton.Left => "左键",
            MouseButton.Right => "右键",
            MouseButton.Middle => "中键",
            _ => Button.ToString(),
        };
        return $"在{where} {button}{(DoubleClick ? "双击" : "单击")}";
    }
}

/// <summary>从一个坐标拖拽到另一个坐标。</summary>
public class MouseDragAction : IAction
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public MouseButton Button { get; set; } = MouseButton.Left;
    public int DurationMs { get; set; } = 300;

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.Drag(FromX, FromY, ToX, ToY, Button, DurationMs);
        return Task.CompletedTask;
    }

    public string Describe() => $"从 ({FromX}, {FromY}) 拖拽到 ({ToX}, {ToY})，耗时 {DurationMs} 毫秒";
}

/// <summary>滚动鼠标滚轮。Delta 为正表示向上/向左，为负表示向下/向右，单位为“格”。</summary>
public class MouseScrollAction : IAction
{
    public int Delta { get; set; } = -3;
    public bool Horizontal { get; set; }

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.Scroll(Delta, Horizontal);
        return Task.CompletedTask;
    }

    public string Describe() => $"{(Horizontal ? "水平" : "垂直")}滚动 {Math.Abs(Delta)} 格{(Delta >= 0 ? "（上/左）" : "（下/右）")}";
}
