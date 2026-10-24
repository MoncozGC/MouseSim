using MouseSim.Core.Hotkeys;

namespace MouseSim.Core.Persistence;

/// <summary>应用设置。当前包含全局热键配置。</summary>
public class AppSettings
{
    public HotkeyDefinition StartHotkey { get; set; } = new() { VirtualKey = 0x75 };  // F6
    public HotkeyDefinition StopHotkey { get; set; } = new() { VirtualKey = 0x77 };  // F8
    public HotkeyDefinition RecordHotkey { get; set; } = new() { VirtualKey = 0x78 }; // F9
}
