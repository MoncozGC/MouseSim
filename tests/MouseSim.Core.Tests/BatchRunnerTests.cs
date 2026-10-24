using MouseSim.Core.Actions;
using MouseSim.Core.Engine;
using MouseSim.Core.Input;
using MouseSim.Core.Models;
using MouseSim.Core.Tests;

namespace MouseSim.Core.Tests;

public class BatchRunnerTests
{
    private static Script ClickScript(string name, string marker, int loopCount = 1) => new()
    {
        Name = name,
        LoopCount = loopCount,
        Actions = [new DelegateAction((sim, _, _) => { sim.TypeText(marker); return Task.CompletedTask; })],
    };

    [Fact]
    public async Task RunsScriptsInOrder()
    {
        var sim = new FakeInputSimulator();
        var scripts = new[] { ClickScript("A", "a"), ClickScript("B", "b"), ClickScript("C", "c") };

        await new BatchRunner(sim).RunAsync(scripts, loops: 1, CancellationToken.None);

        Assert.Equal(new[] { "text:a", "text:b", "text:c" }, sim.Ops);
    }

    [Fact]
    public async Task LoopsWholeBatch()
    {
        var sim = new FakeInputSimulator();
        var scripts = new[] { ClickScript("A", "a"), ClickScript("B", "b") };

        await new BatchRunner(sim).RunAsync(scripts, loops: 2, CancellationToken.None);

        Assert.Equal(new[] { "text:a", "text:b", "text:a", "text:b" }, sim.Ops);
    }

    [Fact]
    public async Task EachScriptKeepsItsOwnLoopCount()
    {
        var sim = new FakeInputSimulator();
        var scripts = new[] { ClickScript("A", "a", loopCount: 3) };

        await new BatchRunner(sim).RunAsync(scripts, loops: 1, CancellationToken.None);

        Assert.Equal(3, sim.Ops.Count);
    }

    [Fact]
    public async Task EmptyListThrows()
    {
        var runner = new BatchRunner(new FakeInputSimulator());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync([], loops: 1, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationBetweenScriptsStopsBatch()
    {
        var sim = new FakeInputSimulator();
        using var cts = new CancellationTokenSource();
        var scripts = new[]
        {
            ClickScript("A", "a"),
            new Script
            {
                Name = "cancel",
                Actions = [new DelegateAction((_, _, _) => { cts.Cancel(); return Task.CompletedTask; })],
            },
            ClickScript("C", "c"), // 不应执行
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new BatchRunner(sim).RunAsync(scripts, loops: 1, cts.Token));

        Assert.Equal(new[] { "text:a" }, sim.Ops);
    }

    [Fact]
    public async Task StageStartedReportsEachScript()
    {
        var sim = new FakeInputSimulator();
        var runner = new BatchRunner(sim);
        var stages = new List<string>();
        runner.StageStarted += (loop, index, script) => stages.Add($"{loop}:{index}:{script.Name}");

        var scripts = new[] { ClickScript("A", "a"), ClickScript("B", "b") };
        await runner.RunAsync(scripts, loops: 2, CancellationToken.None);

        Assert.Equal(new[] { "0:0:A", "0:1:B", "1:0:A", "1:1:B" }, stages);
    }

    [Fact]
    public async Task DurationLimitOverridesLoops()
    {
        var sim = new FakeInputSimulator();
        // 整批循环故意设为无限，时长约束应让它停止
        var scripts = new[]
        {
            new Script { Name = "A", Actions = [new DelayAction { Milliseconds = 60 }] },
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new BatchRunner(sim).RunAsync(scripts, loops: 0, CancellationToken.None, TimeSpan.FromMilliseconds(250));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 250 && sw.ElapsedMilliseconds < 2500,
            $"实际耗时 {sw.ElapsedMilliseconds}ms，应在约 250ms 停止");
    }
}
