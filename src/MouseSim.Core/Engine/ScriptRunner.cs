using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Models;

namespace MouseSim.Core.Engine;

/// <summary>按顺序执行脚本动作，支持循环与取消。</summary>
public sealed class ScriptRunner(IInputSimulator simulator)
{
    /// <summary>每执行一个动作触发一次（loopIndex、actionIndex 均从 0 开始）。</summary>
    public event Action<int, int, IAction>? Progress;

    /// <param name="durationLimit">重复时长限制；设置后优先于 LoopCount，到时停止（至少执行一轮）。</param>
    public async Task RunAsync(Script script, CancellationToken cancellationToken, TimeSpan? durationLimit = null)
    {
        if (script.Actions.Count == 0)
            throw new InvalidOperationException("脚本是空的，请先添加动作");

        // 按脚本自带的速度倍率缩放（倍率 1 时原样返回，无拷贝开销）
        script = PlaybackScaler.Scale(script);

        var sw = durationLimit.HasValue ? System.Diagnostics.Stopwatch.StartNew() : null;
        int loop = 0;
        while (!cancellationToken.IsCancellationRequested && ShouldContinue())
        {
            for (int i = 0; i < script.Actions.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var action = script.Actions[i];
                Progress?.Invoke(loop, i, action);
                await action.ExecuteAsync(simulator, cancellationToken);

                if (script.StepDelayMs > 0)
                    await Task.Delay(script.StepDelayMs, cancellationToken);
            }
            loop++;
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool ShouldContinue()
        {
            if (durationLimit is { } limit)
                return sw!.Elapsed < limit;
            return script.LoopCount == 0 || loop < script.LoopCount;
        }
    }
}
