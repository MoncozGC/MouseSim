using MouseSim.Core.Actions;
using MouseSim.Core.Input;

namespace MouseSim.Core.Tests;

/// <summary>记录所有调用的假输入模拟器，用于验证引擎逻辑而不产生真实输入。</summary>
public sealed class FakeInputSimulator : IInputSimulator
{
    public List<string> Ops { get; } = [];

    public void MoveTo(int x, int y) => Ops.Add($"move:{x},{y}");

    public void MouseDown(MouseButton button) => Ops.Add($"down:{button}");

    public void MouseUp(MouseButton button) => Ops.Add($"up:{button}");

    public void Click(MouseButton button)
    {
        Ops.Add($"down:{button}");
        Ops.Add($"up:{button}");
    }

    public void DoubleClick(MouseButton button)
    {
        Click(button);
        Click(button);
    }

    public void Drag(int fromX, int fromY, int toX, int toY, MouseButton button, int durationMs)
    {
        Ops.Add($"drag:{fromX},{fromY}->{toX},{toY}:{button}:{durationMs}");
    }

    public void Scroll(int delta, bool horizontal) => Ops.Add($"scroll:{delta}:{(horizontal ? "h" : "v")}");

    public void KeyDown(int virtualKey) => Ops.Add($"keydown:{virtualKey}");

    public void KeyUp(int virtualKey) => Ops.Add($"keyup:{virtualKey}");

    public void PressKey(KeyModifiers modifiers, int virtualKey) => Ops.Add($"press:{modifiers}:{virtualKey}");

    public void TypeText(string text) => Ops.Add($"text:{text}");
}

/// <summary>测试用动作：自定义行为（例如在执行几次后取消令牌）。</summary>
public sealed class DelegateAction(Func<IInputSimulator, CancellationToken, int, Task> run) : IAction
{
    private int _count;

    /// <summary>已执行次数。</summary>
    public int Count => _count;

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        var n = _count++;
        return run(simulator, cancellationToken, n);
    }

    public string Describe() => "测试动作";
}
