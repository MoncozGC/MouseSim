namespace MouseSim.App.Infrastructure;

/// <summary>通用下拉选项：值 + 显示名（用于非枚举场景，如“分钟/小时”）。</summary>
public sealed class OptionItem<T>(T value, string label)
{
    public T Value { get; } = value;
    public string Label { get; } = label;

    public override string ToString() => Label;
}
