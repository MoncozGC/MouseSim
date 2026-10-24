using MouseSim.Core.Input;

namespace MouseSim.Core.Models;

public enum ClickerPositionMode
{
    CurrentCursor = 0,
    Fixed = 1,
}

/// <summary>连点器配置。RepeatCount = 0 表示无限次。</summary>
public class ClickerConfig
{
    public int IntervalMs { get; set; } = 100;
    public MouseButton Button { get; set; } = MouseButton.Left;
    public ClickerPositionMode PositionMode { get; set; } = ClickerPositionMode.CurrentCursor;
    public int X { get; set; }
    public int Y { get; set; }
    public bool DoubleClick { get; set; }
    public int RepeatCount { get; set; }
}
