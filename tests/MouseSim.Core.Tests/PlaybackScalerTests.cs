using MouseSim.Core.Actions;
using MouseSim.Core.Engine;
using MouseSim.Core.Input;
using MouseSim.Core.Models;

namespace MouseSim.Core.Tests;

public class PlaybackScalerTests
{
    [Fact]
    public void SpeedOneReturnsOriginalInstance()
    {
        var script = new Script { SpeedMultiplier = 1.0, Actions = [new DelayAction { Milliseconds = 500 }] };

        var scaled = PlaybackScaler.Scale(script);

        Assert.Same(script, scaled);
    }

    [Fact]
    public void DoubleSpeedHalvesDelays()
    {
        var script = new Script
        {
            SpeedMultiplier = 2.0,
            StepDelayMs = 200,
            Actions =
            [
                new DelayAction { Milliseconds = 1000 },
                new MouseDragAction { DurationMs = 400 },
                new KeyHoldAction { DurationMs = 300 },
                new MouseClickAction(), // 不受速度影响
            ],
        };

        var scaled = PlaybackScaler.Scale(script);

        Assert.NotSame(script, scaled);
        Assert.Equal(100, scaled.StepDelayMs);
        Assert.Equal(500, Assert.IsType<DelayAction>(scaled.Actions[0]).Milliseconds);
        Assert.Equal(200, Assert.IsType<MouseDragAction>(scaled.Actions[1]).DurationMs);
        Assert.Equal(150, Assert.IsType<KeyHoldAction>(scaled.Actions[2]).DurationMs);
        Assert.IsType<MouseClickAction>(scaled.Actions[3]);
        // 原脚本不被修改
        Assert.Equal(1000, Assert.IsType<DelayAction>(script.Actions[0]).Milliseconds);
    }

    [Fact]
    public void HalfSpeedDoublesDelays()
    {
        var script = new Script
        {
            SpeedMultiplier = 0.5,
            Actions = [new DelayAction { Milliseconds = 250 }],
        };

        var scaled = PlaybackScaler.Scale(script);

        Assert.Equal(500, Assert.IsType<DelayAction>(scaled.Actions[0]).Milliseconds);
    }

    [Fact]
    public void DragDurationKeepsMinimum()
    {
        var script = new Script
        {
            SpeedMultiplier = 4.0,
            Actions = [new MouseDragAction { DurationMs = 100 }],
        };

        var scaled = PlaybackScaler.Scale(script);

        // 100/4=25，但拖拽时长有 50ms 下限
        Assert.Equal(50, Assert.IsType<MouseDragAction>(scaled.Actions[0]).DurationMs);
    }

    [Fact]
    public async Task RunnerRespectsSpeed()
    {
        var sim = new FakeInputSimulator();
        var script = new Script
        {
            SpeedMultiplier = 4.0,
            LoopCount = 2,
            Actions = [new DelayAction { Milliseconds = 400 }],
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None);
        sw.Stop();

        // 常速需 ~800ms，×4 后应 < 600ms（留调度余量）
        Assert.True(sw.ElapsedMilliseconds < 600, $"实际耗时 {sw.ElapsedMilliseconds}ms");
    }
}
