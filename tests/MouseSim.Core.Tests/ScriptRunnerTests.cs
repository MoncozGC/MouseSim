using MouseSim.Core.Actions;
using MouseSim.Core.Engine;
using MouseSim.Core.Input;
using MouseSim.Core.Models;
using MouseSim.Core.Tests;

namespace MouseSim.Core.Tests;

public class ScriptRunnerTests
{
    private static Script MakeScript(int loopCount, params IAction[] actions) => new()
    {
        Name = "测试",
        LoopCount = loopCount,
        Actions = [.. actions],
    };

    [Fact]
    public async Task ExecutesActionsInOrder()
    {
        var sim = new FakeInputSimulator();
        var script = MakeScript(1,
            new MouseMoveAction { X = 10, Y = 20 },
            new MouseClickAction { Button = MouseButton.Left, X = 10, Y = 20 },
            new KeyPressAction { Modifiers = KeyModifiers.Control, VirtualKey = 0x53 });

        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None);

        Assert.Equal(new[]
        {
            "move:10,20",
            "move:10,20", // 点击动作先移动到目标坐标
            "down:Left", "up:Left",
            "press:Control:83",
        }, sim.Ops);
    }

    [Fact]
    public async Task LoopsSpecifiedTimes()
    {
        var sim = new FakeInputSimulator();
        var script = MakeScript(3, new MouseClickAction());

        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None);

        Assert.Equal(3, sim.Ops.Count(o => o == "down:Left"));
    }

    [Fact]
    public async Task LoopCountZeroRunsForeverUntilCancelled()
    {
        var sim = new FakeInputSimulator();
        using var cts = new CancellationTokenSource();

        // 第 3 次执行后请求取消
        var action = new DelegateAction((_, ct, n) =>
        {
            if (n == 2) cts.Cancel();
            return Task.CompletedTask;
        });
        var script = MakeScript(0, action);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ScriptRunner(sim).RunAsync(script, cts.Token));

        Assert.Equal(3, action.Count);
    }

    [Fact]
    public async Task StepDelayIsAppliedAfterEachAction()
    {
        var sim = new FakeInputSimulator();
        var script = MakeScript(1, new DelayAction { Milliseconds = 1 }, new DelayAction { Milliseconds = 1 });
        script.StepDelayMs = 120;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None);
        sw.Stop();

        // 两个动作各 1ms + 每步延迟 120ms*2，留出调度误差余量
        Assert.True(sw.ElapsedMilliseconds >= 200, $"实际耗时 {sw.ElapsedMilliseconds}ms，应 >= 200ms");
    }

    [Fact]
    public async Task EmptyScriptThrows()
    {
        var runner = new ScriptRunner(new FakeInputSimulator());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(new Script(), CancellationToken.None));
    }

    [Fact]
    public async Task DurationLimitOverridesLoopCount()
    {
        var sim = new FakeInputSimulator();
        // 每轮至少耗时 ~50ms，循环上限故意设很大；200ms 时长约束应让它提前停止
        var script = MakeScript(1_000_000, new DelayAction { Milliseconds = 50 });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None, TimeSpan.FromMilliseconds(200));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 200 && sw.ElapsedMilliseconds < 2000,
            $"实际耗时 {sw.ElapsedMilliseconds}ms，应在约 200ms 停止");
    }

    [Fact]
    public async Task DurationLimitRunsAtLeastOneLoop()
    {
        var sim = new FakeInputSimulator();
        var script = MakeScript(1, new MouseClickAction());

        // 时长极小也要先完整跑完一轮
        await new ScriptRunner(sim).RunAsync(script, CancellationToken.None, TimeSpan.FromMilliseconds(1));

        Assert.Contains("down:Left", sim.Ops);
    }

    [Fact]
    public async Task ProgressReportsLoopAndActionIndex()
    {
        var sim = new FakeInputSimulator();
        var runner = new ScriptRunner(sim);
        var progress = new List<(int loop, int index)>();
        runner.Progress += (loop, index, _) => progress.Add((loop, index));

        var script = MakeScript(2, new MouseMoveAction(), new DelayAction { Milliseconds = 1 });
        await runner.RunAsync(script, CancellationToken.None);

        Assert.Equal(new[]
        {
            (0, 0), (0, 1),
            (1, 0), (1, 1),
        }, progress);
    }
}
