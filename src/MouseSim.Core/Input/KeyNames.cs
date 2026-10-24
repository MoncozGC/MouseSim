namespace MouseSim.Core.Input;

/// <summary>虚拟键码 → 显示名。覆盖常用键，未收录的键回退为 "VK 0x??"。</summary>
public static class KeyNames
{
    private static readonly Dictionary<int, string> Map = BuildMap();

    public static string Get(int virtualKey)
        => Map.TryGetValue(virtualKey, out var name) ? name : $"VK 0x{virtualKey:X2}";

    private static Dictionary<int, string> BuildMap()
    {
        var map = new Dictionary<int, string>();

        // 字母 A-Z：0x41-0x5A
        for (int vk = 0x41; vk <= 0x5A; vk++)
            map[vk] = ((char)vk).ToString();

        // 数字 0-9：0x30-0x39
        for (int vk = 0x30; vk <= 0x39; vk++)
            map[vk] = ((char)vk).ToString();

        // 功能键 F1-F24：0x70-0x87
        for (int n = 1; n <= 24; n++)
            map[0x70 + n - 1] = $"F{n}";

        // 小键盘 0-9：0x60-0x69
        for (int n = 0; n <= 9; n++)
            map[0x60 + n] = $"小键盘{n}";

        var named = new (int vk, string name)[]
        {
            (0x01, "左键"), (0x02, "右键"), (0x04, "中键"),
            (0x08, "Backspace"), (0x09, "Tab"), (0x0D, "Enter"), (0x13, "Pause"),
            (0x14, "CapsLock"), (0x1B, "Esc"), (0x20, "空格"),
            (0x21, "PageUp"), (0x22, "PageDown"), (0x23, "End"), (0x24, "Home"),
            (0x25, "←"), (0x26, "↑"), (0x27, "→"), (0x28, "↓"),
            (0x2C, "PrintScreen"), (0x2D, "Insert"), (0x2E, "Delete"),
            (0x5D, "菜单键"), (0x5B, "Win"),
            (0x6A, "小键盘*"), (0x6B, "小键盘+"), (0x6D, "小键盘-"), (0x6E, "小键盘."), (0x6F, "小键盘/"),
            (0x90, "NumLock"), (0x91, "ScrollLock"),
            (0xA0, "左Shift"), (0xA1, "右Shift"), (0xA2, "左Ctrl"), (0xA3, "右Ctrl"),
            (0xA4, "左Alt"), (0xA5, "右Alt"),
            (0xBA, ";"), (0xBB, "="), (0xBC, ","), (0xBD, "-"), (0xBE, "."), (0xBF, "/"),
            (0xC0, "`"), (0xDB, "["), (0xDC, "\\"), (0xDD, "]"), (0xDE, "'"),
        };

        foreach (var (vk, name) in named)
            map[vk] = name;

        return map;
    }
}
