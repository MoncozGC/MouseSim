namespace MouseSim.Core.Input;

public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

[Flags]
public enum KeyModifiers
{
    None = 0,

    // 位定义与 Win32 MOD_* 常量保持一致，便于直接转换为 RegisterHotKey 参数
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}
