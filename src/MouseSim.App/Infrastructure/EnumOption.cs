namespace MouseSim.App.Infrastructure;

/// <summary>枚举选项：值 + 界面显示名，供 ComboBox 使用。</summary>
public sealed class EnumOption<T>(T value, string label) where T : struct, Enum
{
    public T Value { get; } = value;
    public string Label { get; } = label;

    public override string ToString() => Label;
}
