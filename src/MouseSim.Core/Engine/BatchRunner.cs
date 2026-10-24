using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Models;

namespace MouseSim.Core.Engine;

/// <summary>
/// 批量执行多个脚本：按列表顺序逐个运行，每个脚本各自的循环次数/每步延迟生效，
/// 整批可再循环 loops 次（0 = 无限）。
/// </summary>
public sealed class BatchRunner(IInputSimulator simulator)
{
    /// <summary>每个脚本开始执行时触发（loopIndex、scriptIndex 从 0 开始）。</summary>
    public event Action<int, int, Script>? StageStarted;

    /// <summary>单个脚本内部的执行进度（透传 ScriptRunner.Progress）。</summary>
    public event Action<Script, int, int, IAction>? ScriptProgress;

    /// <param name="loops">整批循环次数，0 = 无限。</param>
    /// <param name="durationLimit">重复时长限制；设置后优先于 loops，到时停止（至少执行一轮）。</param>
    public async Task RunAsync(IReadOnlyList<Script> scripts, int loops, CancellationToken cancellationToken, TimeSpan? durationLimit = null)
    {
        if (scripts.Count == 0)
            throw new InvalidOperationException("没有勾选任何脚本");

        var sw = durationLimit.HasValue ? System.Diagnostics.Stopwatch.StartNew() : null;
        int loop = 0;
        while (!cancellationToken.IsCancellationRequested && ShouldContinue())
        {
            for (int i = 0; i < scripts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StageStarted?.Invoke(loop, i, scripts[i]);

                var runner = new ScriptRunner(simulator);
                runner.Progress += (actionLoop, actionIndex, action) =>
                    ScriptProgress?.Invoke(scripts[i], actionLoop, actionIndex, action);
                await runner.RunAsync(scripts[i], cancellationToken);
            }
            loop++;
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool ShouldContinue()
        {
            if (durationLimit is { } limit)
                return sw!.Elapsed < limit;
            return loops == 0 || loop < loops;
        }
    }
}
