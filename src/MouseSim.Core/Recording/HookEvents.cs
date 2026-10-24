namespace MouseSim.Core.Recording;

public enum MouseHookEventType
{
    Move,
    LeftDown,
    LeftUp,
    RightDown,
    RightUp,
    MiddleDown,
    MiddleUp,
    Scroll,
    ScrollHorizontal,
}

/// <summary>低级鼠标钩子事件。Timestamp 为系统启动以来的毫秒数。
/// ExtraInfo 是注入方携带的标记：等于 InputSource.OwnSendMarker 表示是本程序回放注入的。</summary>
public sealed record MouseHookEvent(
    MouseHookEventType Type,
    int X,
    int Y,
    int WheelDelta,
    long Timestamp,
    bool Injected = false,
    IntPtr ExtraInfo = default);

/// <summary>低级键盘钩子事件。</summary>
public sealed record KeyHookEvent(
    bool IsDown,
    int VirtualKey,
    long Timestamp,
    bool Injected = false,
    IntPtr ExtraInfo = default);
