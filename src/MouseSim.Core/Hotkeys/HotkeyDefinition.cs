using System.Text.Json.Serialization;
using MouseSim.Core.Input;

namespace MouseSim.Core.Hotkeys;

/// <summary>一个全局热键定义（修饰键 + 虚拟键码）。</summary>
public class HotkeyDefinition
{
    public KeyModifiers Modifiers { get; set; }
    public int VirtualKey { get; set; }

    [JsonIgnore]
    public string Display
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
            parts.Add(KeyNames.Get(VirtualKey));
            return string.Join("+", parts);
        }
    }

    public override string ToString() => Display;
}
