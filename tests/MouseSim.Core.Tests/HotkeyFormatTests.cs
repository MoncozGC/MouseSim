using MouseSim.Core.Hotkeys;
using MouseSim.Core.Input;

namespace MouseSim.Core.Tests;

public class HotkeyFormatTests
{
    [Fact]
    public void DisplayIncludesModifiersInCanonicalOrder()
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = KeyModifiers.Shift | KeyModifiers.Win | KeyModifiers.Control | KeyModifiers.Alt,
            VirtualKey = 0x75, // F6
        };

        Assert.Equal("Ctrl+Alt+Shift+Win+F6", hotkey.Display);
    }

    [Fact]
    public void DisplayPlainKeyHasNoModifier()
    {
        var hotkey = new HotkeyDefinition { VirtualKey = 0x77 }; // F8
        Assert.Equal("F8", hotkey.Display);
    }

    [Fact]
    public void DisplayLetterKey()
    {
        var hotkey = new HotkeyDefinition { Modifiers = KeyModifiers.Control, VirtualKey = 0x53 }; // S
        Assert.Equal("Ctrl+S", hotkey.Display);
    }
}
