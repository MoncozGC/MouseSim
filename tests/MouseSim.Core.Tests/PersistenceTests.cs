using System.Text.Json;
using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Models;
using MouseSim.Core.Persistence;

namespace MouseSim.Core.Tests;

public class PersistenceTests
{
    [Fact]
    public void ScriptWithAllActionTypesRoundTrips()
    {
        var script = new Script
        {
            Name = "完整脚本",
            LoopCount = 5,
            StepDelayMs = 120,
            RepeatMinutes = 90,
            Actions =
            [
                new MouseMoveAction { X = -100, Y = 200 },
                new MouseClickAction { Button = MouseButton.Right, X = 30, Y = 40, DoubleClick = true },
                new MouseDragAction { FromX = 1, FromY = 2, ToX = 3, ToY = 4, DurationMs = 500 },
                new MouseScrollAction { Delta = -7, Horizontal = true },
                new KeyPressAction { Modifiers = KeyModifiers.Control | KeyModifiers.Alt, VirtualKey = 0x73 },
                new KeyHoldAction { VirtualKey = 0x10, DurationMs = 2500 },
                new TextInputAction { Text = "你好 MouseSim\n第二行" },
                new DelayAction { Milliseconds = 900 },
            ],
        };

        var json = JsonSerializer.Serialize(script, AppDataStore.JsonOpts);
        var restored = JsonSerializer.Deserialize<Script>(json, AppDataStore.JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal("完整脚本", restored!.Name);
        Assert.Equal(5, restored.LoopCount);
        Assert.Equal(120, restored.StepDelayMs);
        Assert.Equal(90, restored.RepeatMinutes);
        Assert.Equal(8, restored.Actions.Count);

        var move = Assert.IsType<MouseMoveAction>(restored.Actions[0]);
        Assert.Equal((-100, 200), (move.X, move.Y));

        var click = Assert.IsType<MouseClickAction>(restored.Actions[1]);
        Assert.Equal(MouseButton.Right, click.Button);
        Assert.True(click.DoubleClick);
        Assert.Equal((30, 40), (click.X, click.Y));

        var drag = Assert.IsType<MouseDragAction>(restored.Actions[2]);
        Assert.Equal(500, drag.DurationMs);

        var scroll = Assert.IsType<MouseScrollAction>(restored.Actions[3]);
        Assert.Equal(-7, scroll.Delta);
        Assert.True(scroll.Horizontal);

        var key = Assert.IsType<KeyPressAction>(restored.Actions[4]);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt, key.Modifiers);
        Assert.Equal(0x73, key.VirtualKey);

        var hold = Assert.IsType<KeyHoldAction>(restored.Actions[5]);
        Assert.Equal(2500, hold.DurationMs);

        var text = Assert.IsType<TextInputAction>(restored.Actions[6]);
        Assert.Equal("你好 MouseSim\n第二行", text.Text);

        var delay = Assert.IsType<DelayAction>(restored.Actions[7]);
        Assert.Equal(900, delay.Milliseconds);
    }

    [Fact]
    public void AppSettingsRoundTrips()
    {
        var settings = new AppSettings
        {
            StartHotkey = new Hotkeys.HotkeyDefinition { Modifiers = KeyModifiers.Control | KeyModifiers.Shift, VirtualKey = 0x75 },
            StopHotkey = new Hotkeys.HotkeyDefinition { VirtualKey = 0x1B },
        };

        var json = JsonSerializer.Serialize(settings, AppDataStore.JsonOpts);
        var restored = JsonSerializer.Deserialize<AppSettings>(json, AppDataStore.JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, restored!.StartHotkey.Modifiers);
        Assert.Equal(0x75, restored.StartHotkey.VirtualKey);
        Assert.Equal(0x1B, restored.StopHotkey.VirtualKey);
    }
}
