using System.Text.Json.Serialization;
using MouseSim.Core.Input;

namespace MouseSim.Core.Actions;

/// <summary>
/// 一个可执行的动作。动作是脚本的最小组成单位，
/// 通过 JSON 多态序列化保存（$kind 字段区分子类型）。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(MouseMoveAction), "move")]
[JsonDerivedType(typeof(MouseClickAction), "click")]
[JsonDerivedType(typeof(MouseDragAction), "drag")]
[JsonDerivedType(typeof(MouseScrollAction), "scroll")]
[JsonDerivedType(typeof(KeyPressAction), "keypress")]
[JsonDerivedType(typeof(KeyHoldAction), "keyhold")]
[JsonDerivedType(typeof(TextInputAction), "text")]
[JsonDerivedType(typeof(DelayAction), "delay")]
public interface IAction
{
    /// <summary>执行动作。ct 取消时应抛出 OperationCanceledException。</summary>
    Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken);

    /// <summary>人类可读的描述，用于界面动作列表展示。</summary>
    string Describe();
}

/// <summary>动作类型的界面标签（供 UI 显示，不参与序列化）。</summary>
public static class ActionTypeLabels
{
    public static string Of(IAction action) => action switch
    {
        MouseMoveAction => "移动",
        MouseClickAction => "点击",
        MouseDragAction => "拖拽",
        MouseScrollAction => "滚轮",
        KeyPressAction => "按键",
        KeyHoldAction => "按住",
        TextInputAction => "文本",
        DelayAction => "延迟",
        _ => action.GetType().Name,
    };
}

/// <summary>等待指定毫秒数。</summary>
public class DelayAction : IAction
{
    public int Milliseconds { get; set; } = 500;

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Delay(Math.Max(Milliseconds, 0), cancellationToken);
    }

    public string Describe() => $"等待 {Milliseconds} 毫秒";
}
