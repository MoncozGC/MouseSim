using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Recording;

namespace MouseSim.Core.Tests;

public class MacroRecorderTests
{
    private static MouseHookEvent Mouse(MouseHookEventType type, int x, int y, long t, int delta = 0)
        => new(type, x, y, delta, t);

    private static KeyHookEvent Key(bool down, int vk, long t) => new(down, vk, t);

    [Fact]
    public void SingleClickBecomesClickAction()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 100, 200, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 101, 200, 1100));

        var actions = recorder.Stop();

        var click = Assert.IsType<MouseClickAction>(Assert.Single(actions));
        Assert.Equal(MouseButton.Left, click.Button);
        Assert.Equal((100, 200), (click.X, click.Y));
        Assert.False(click.DoubleClick);
    }

    [Fact]
    public void TwoQuickClicksBecomeDoubleClick()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 50, 60, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 50, 60, 1050));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 51, 61, 1200));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 51, 61, 1250));

        var actions = recorder.Stop();

        var click = Assert.IsType<MouseClickAction>(Assert.Single(actions));
        Assert.True(click.DoubleClick);
    }

    [Fact]
    public void SlowRepeatedClicksStaySeparate()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 50, 60, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 50, 60, 1050));
        // 间隔远超系统双击时间（默认 500ms）
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 50, 60, 5000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 50, 60, 5050));

        var actions = recorder.Stop();

        Assert.Equal(2, actions.Count(a => a is MouseClickAction));
    }

    [Fact]
    public void PressMoveReleaseBecomesDrag()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 10, 10, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.Move, 40, 30, 1100));
        recorder.OnMouse(Mouse(MouseHookEventType.Move, 100, 100, 1300));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 110, 110, 1500));

        var actions = recorder.Stop();

        var drag = Assert.IsType<MouseDragAction>(Assert.Single(actions));
        Assert.Equal((10, 10), (drag.FromX, drag.FromY));
        Assert.Equal((110, 110), (drag.ToX, drag.ToY));
        Assert.Equal(500, drag.DurationMs);
    }

    [Fact]
    public void ConsecutiveScrollsAccumulate()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.Scroll, 30, 40, 1000, -120));
        recorder.OnMouse(Mouse(MouseHookEventType.Scroll, 30, 40, 1100, -120));
        recorder.OnMouse(Mouse(MouseHookEventType.Scroll, 31, 40, 1200, -120));

        var actions = recorder.Stop();

        var scroll = Assert.IsType<MouseScrollAction>(Assert.Single(actions));
        Assert.Equal(-3, scroll.Delta);
        Assert.False(scroll.Horizontal);
    }

    [Fact]
    public void ScrollPositionJumpFlushes()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(Mouse(MouseHookEventType.Scroll, 10, 10, 1000, 120));
        recorder.OnMouse(Mouse(MouseHookEventType.Scroll, 500, 500, 1100, 120));

        var actions = recorder.Stop();

        Assert.Equal(2, actions.Count(a => a is MouseScrollAction));
    }

    [Fact]
    public void ModifierPlusKeyBecomesCombo()
    {
        var recorder = new MacroRecorder();
        recorder.OnKey(Key(true, 0x11, 1000));  // Ctrl down
        recorder.OnKey(Key(true, 0x53, 1100));  // S down
        recorder.OnKey(Key(false, 0x53, 1200)); // S up
        recorder.OnKey(Key(false, 0x11, 1300)); // Ctrl up

        var actions = recorder.Stop();

        var key = Assert.IsType<KeyPressAction>(Assert.Single(actions));
        Assert.Equal(KeyModifiers.Control, key.Modifiers);
        Assert.Equal(0x53, key.VirtualKey);
    }

    [Fact]
    public void KeyAutoRepeatRecordsOnce()
    {
        var recorder = new MacroRecorder();
        recorder.OnKey(Key(true, 0x53, 1000));
        recorder.OnKey(Key(true, 0x53, 1050));  // 自动重复的按下
        recorder.OnKey(Key(true, 0x53, 1100));
        recorder.OnKey(Key(false, 0x53, 1200));

        var actions = recorder.Stop();

        Assert.Single(actions.OfType<KeyPressAction>());
    }

    [Fact]
    public void ModifierOnlyPressIsDropped()
    {
        var recorder = new MacroRecorder();
        recorder.OnKey(Key(true, 0x10, 1000));   // Shift down
        recorder.OnKey(Key(false, 0x10, 1200));  // Shift up

        Assert.Empty(recorder.Stop());
    }

    [Fact]
    public void GapBetweenActionsBecomesDelay()
    {
        var recorder = new MacroRecorder(minGapMs: 60);
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 10, 10, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 10, 10, 1050));
        recorder.OnKey(Key(true, 0x41, 5000));   // 3.95 秒之后按 A
        recorder.OnKey(Key(false, 0x41, 5100));

        var actions = recorder.Stop();

        var delay = Assert.Single(actions.OfType<DelayAction>());
        Assert.InRange(delay.Milliseconds, 3000, 4000);
        Assert.Single(actions.OfType<MouseClickAction>());
        Assert.Single(actions.OfType<KeyPressAction>());
    }

    [Fact]
    public void SmallGapDoesNotCreateDelay()
    {
        var recorder = new MacroRecorder(minGapMs: 60);
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 10, 10, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 10, 10, 1050));
        recorder.OnKey(Key(true, 0x41, 1090));
        recorder.OnKey(Key(false, 0x41, 1150));

        var actions = recorder.Stop();

        Assert.Empty(actions.OfType<DelayAction>());
    }

    [Fact]
    public void IgnoredEventsAreNotRecorded()
    {
        var recorder = new MacroRecorder(
            ignoreMouse: e => e.X == 0,       // 忽略 x=0 的鼠标事件
            ignoreKey: e => e.VirtualKey == 0x75); // 忽略 F6
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 0, 0, 1000));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 0, 0, 1050));
        recorder.OnKey(Key(true, 0x75, 1100));
        recorder.OnKey(Key(false, 0x75, 1150));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftDown, 30, 40, 1200));
        recorder.OnMouse(Mouse(MouseHookEventType.LeftUp, 30, 40, 1250));

        var actions = recorder.Stop();

        Assert.Single(actions.OfType<MouseClickAction>());
        Assert.Empty(actions.OfType<KeyPressAction>());
    }

    [Fact]
    public void OwnReplayEventsAreSkipped()
    {
        var recorder = new MacroRecorder();
        recorder.OnMouse(new MouseHookEvent(MouseHookEventType.LeftDown, 10, 20, 0, 1000, ExtraInfo: InputSource.OwnSendMarker));
        recorder.OnMouse(new MouseHookEvent(MouseHookEventType.LeftUp, 10, 20, 0, 1100, ExtraInfo: InputSource.OwnSendMarker));

        Assert.Empty(recorder.Stop());
    }

    [Fact]
    public void InjectedByOtherSoftwareIsRecorded()
    {
        // 鼠标/改键驱动注入的输入带 INJECTED 标志，但对用户而言就是真实操作，应录制
        var recorder = new MacroRecorder();
        recorder.OnMouse(new MouseHookEvent(MouseHookEventType.LeftDown, 10, 20, 0, 1000, Injected: true));
        recorder.OnMouse(new MouseHookEvent(MouseHookEventType.LeftUp, 10, 20, 0, 1100, Injected: true));

        var actions = recorder.Stop();
        Assert.IsType<MouseClickAction>(Assert.Single(actions));
    }

    [Fact]
    public void TextTypingRecordsEachKey()
    {
        var recorder = new MacroRecorder();
        foreach (var (vk, t) in new[] { (0x48, 1000), (0x49, 1100) }) // H I
        {
            recorder.OnKey(Key(true, vk, t));
            recorder.OnKey(Key(false, vk, t + 40));
        }

        var actions = recorder.Stop();

        var keys = actions.OfType<KeyPressAction>().ToList();
        Assert.Equal(2, keys.Count);
        Assert.Equal(0x48, keys[0].VirtualKey);
        Assert.Equal(0x49, keys[1].VirtualKey);
        Assert.All(keys, k => Assert.Equal(KeyModifiers.None, k.Modifiers));
    }
}
