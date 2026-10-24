namespace MouseSim.Core.Input;

/// <summary>本程序注入输入的标记：SendInput 的 dwExtraInfo 填此魔数，
/// 录制器据此识别并跳过自己回放的事件，其他程序的注入输入（如鼠标驱动）不受影响。</summary>
public static class InputSource
{
    public static readonly IntPtr OwnSendMarker = new(0x53494D53); // "SIMS"
}

/// <summary>
/// 底层输入模拟接口。所有方法都在调用线程上同步执行（含内部的微小间隔），
/// 便于单元测试时用假实现替换。
/// </summary>
public interface IInputSimulator
{
    void MoveTo(int x, int y);
    void MouseDown(MouseButton button);
    void MouseUp(MouseButton button);
    void Click(MouseButton button);
    void DoubleClick(MouseButton button);
    void Drag(int fromX, int fromY, int toX, int toY, MouseButton button, int durationMs);
    void Scroll(int delta, bool horizontal);
    void KeyDown(int virtualKey);
    void KeyUp(int virtualKey);
    void PressKey(KeyModifiers modifiers, int virtualKey);
    void TypeText(string text);
}
