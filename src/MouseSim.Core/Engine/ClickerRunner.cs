using MouseSim.Core.Input;
using MouseSim.Core.Models;

namespace MouseSim.Core.Engine;

/// <summary>自动连点循环，支持固定位置/当前位置、有限/无限次数。</summary>
public sealed class ClickerRunner(IInputSimulator simulator)
{
    /// <summary>每完成一次点击触发一次，参数为累计次数。</summary>
    public event Action<int>? Clicked;

    public async Task RunAsync(ClickerConfig config, CancellationToken cancellationToken)
    {
        int interval = Math.Max(config.IntervalMs, 10); // Task.Delay 实际精度约 15ms
        int done = 0;

        while (!cancellationToken.IsCancellationRequested && (config.RepeatCount == 0 || done < config.RepeatCount))
        {
            if (config.PositionMode == ClickerPositionMode.Fixed)
                simulator.MoveTo(config.X, config.Y);

            if (config.DoubleClick) simulator.DoubleClick(config.Button);
            else simulator.Click(config.Button);

            done++;
            Clicked?.Invoke(done);

            if (config.RepeatCount == 0 || done < config.RepeatCount)
                await Task.Delay(interval, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
