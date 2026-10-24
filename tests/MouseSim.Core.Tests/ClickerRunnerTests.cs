using MouseSim.Core.Engine;
using MouseSim.Core.Input;
using MouseSim.Core.Models;
using MouseSim.Core.Tests;

namespace MouseSim.Core.Tests;

public class ClickerRunnerTests
{
    [Fact]
    public async Task FixedCountClicksExactTimes()
    {
        var sim = new FakeInputSimulator();
        var config = new ClickerConfig { IntervalMs = 10, RepeatCount = 4 };

        await new ClickerRunner(sim).RunAsync(config, CancellationToken.None);

        Assert.Equal(4, sim.Ops.Count(o => o == "down:Left"));
    }

    [Fact]
    public async Task FixedPositionMovesBeforeEachClick()
    {
        var sim = new FakeInputSimulator();
        var config = new ClickerConfig
        {
            IntervalMs = 10,
            RepeatCount = 2,
            PositionMode = ClickerPositionMode.Fixed,
            X = 111,
            Y = 222,
        };

        await new ClickerRunner(sim).RunAsync(config, CancellationToken.None);

        Assert.Equal(new[]
        {
            "move:111,222", "down:Left", "up:Left",
            "move:111,222", "down:Left", "up:Left",
        }, sim.Ops);
    }

    [Fact]
    public async Task IntervalIsRespected()
    {
        var sim = new FakeInputSimulator();
        var config = new ClickerConfig { IntervalMs = 60, RepeatCount = 4 };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new ClickerRunner(sim).RunAsync(config, CancellationToken.None);
        sw.Stop();

        // 4 次点击之间有 3 次间隔，各 60ms
        Assert.True(sw.ElapsedMilliseconds >= 170, $"实际耗时 {sw.ElapsedMilliseconds}ms，应 >= 170ms");
    }

    [Fact]
    public async Task DoubleClickDoublesEvents()
    {
        var sim = new FakeInputSimulator();
        var config = new ClickerConfig { IntervalMs = 10, RepeatCount = 1, DoubleClick = true };

        await new ClickerRunner(sim).RunAsync(config, CancellationToken.None);

        Assert.Equal(2, sim.Ops.Count(o => o == "down:Left"));
    }

    [Fact]
    public async Task ClickedEventReportsCount()
    {
        var sim = new FakeInputSimulator();
        var runner = new ClickerRunner(sim);
        var counts = new List<int>();
        runner.Clicked += counts.Add;

        await runner.RunAsync(new ClickerConfig { IntervalMs = 10, RepeatCount = 3 }, CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 3 }, counts);
    }
}
