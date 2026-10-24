using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.Core.Actions;
using MouseSim.Core.Engine;
using MouseSim.Core.Input;
using MouseSim.Core.Models;

namespace MouseSim.App.Services;

/// <summary>
/// 应用内唯一的执行入口：同一时刻只允许一个任务（连点器或脚本）在跑，
/// 全局停止热键取消当前任务。
/// </summary>
public sealed class RunnerService
{
    private readonly IInputSimulator _simulator;
    private readonly StatusViewModel _status;
    private CancellationTokenSource? _cts;

    public RunnerService(IInputSimulator simulator, StatusViewModel status)
    {
        _simulator = simulator;
        _status = status;
    }

    public bool IsRunning => _cts != null;

    /// <summary>当前任务的描述（供悬浮状态卡等展示）；无任务时为 null。</summary>
    public string? CurrentLabel { get; private set; }

    /// <summary>当前任务的重复时长限制（按时长模式）；无或按次数模式为 null。</summary>
    public TimeSpan? CurrentDuration { get; private set; }

    public event EventHandler? RunningChanged;

    /// <summary>任务被用户主动停止（F8 或停止按钮）时触发，用于把主窗口带回前台。</summary>
    public event Action? TaskUserStopped;

    /// <summary>脚本进度（loopIndex、actionIndex 从 0 开始），在后台线程触发。</summary>
    public event Action<int, int, IAction>? ScriptProgress;

    /// <summary>批量进度：第几个脚本（从 0 开始），在后台线程触发。</summary>
    public event Action<int, int, Script>? BatchStage;

    /// <summary>连点器进度（累计次数），在后台线程触发。</summary>
    public event Action<int>? ClickerProgress;

    public bool Start(Script script, TimeSpan? durationLimit = null)
        => StartCore(ScriptLabel(script, durationLimit), durationLimit, async ct =>
        {
            var runner = new ScriptRunner(_simulator);
            runner.Progress += (loop, index, action) => ScriptProgress?.Invoke(loop, index, action);
            await runner.RunAsync(script, ct, durationLimit);
        });

    public bool StartBatch(IReadOnlyList<Script> scripts, int loops, TimeSpan? durationLimit = null)
        => StartCore(BatchLabel(scripts, loops, durationLimit), durationLimit, async ct =>
        {
            var batch = new BatchRunner(_simulator);
            batch.StageStarted += (loop, index, script) =>
            {
                BatchStage?.Invoke(loop, index, script);
                _status.SetText($"批量第 {loop + 1} 轮 · 第 {index + 1}/{scripts.Count} 个：{script.Name}");
            };
            batch.ScriptProgress += (script, loop, index, action) => ScriptProgress?.Invoke(loop, index, action);
            await batch.RunAsync(scripts, loops, ct, durationLimit);
        });

    private static string ScriptLabel(Script script, TimeSpan? duration) => duration is { } d
        ? $"脚本「{script.Name}」重复 {FormatDuration(d)}"
        : $"脚本「{script.Name}」";

    private static string BatchLabel(IReadOnlyList<Script> scripts, int loops, TimeSpan? duration) => duration is { } d
        ? $"批量（{scripts.Count} 个脚本）重复 {FormatDuration(d)}"
        : $"批量（{scripts.Count} 个脚本）";

    private static string FormatDuration(TimeSpan d)
        => d.TotalMinutes >= 60 ? $"{d.TotalHours:0.#} 小时" : $"{d.TotalMinutes:0.#} 分钟";

    public bool Start(ClickerConfig config)
        => StartCore("连点器", null, async ct =>
        {
            var runner = new ClickerRunner(_simulator);
            runner.Clicked += count => ClickerProgress?.Invoke(count);
            await runner.RunAsync(config, ct);
        });

    public void Stop() => _cts?.Cancel();

    /// <summary>供各页面显示提示信息（非法输入等）。</summary>
    public void ReportStatus(string text) => _status.SetText(text);

    private bool StartCore(string label, TimeSpan? durationLimit, Func<CancellationToken, Task> work)
    {
        if (IsRunning)
        {
            _status.SetText("已有任务在运行，请先按停止热键或点击停止");
            return false;
        }

        CurrentLabel = label;
        CurrentDuration = durationLimit;
        var cts = _cts = new CancellationTokenSource();
        RunningChanged?.Invoke(this, EventArgs.Empty);

        _ = Task.Run(async () =>
        {
            try
            {
                await work(cts.Token);
                _status.SetIdle($"{label} 已完成");
            }
            catch (OperationCanceledException)
            {
                _status.SetIdle($"{label} 已停止");
                Ui.OnUi(() => TaskUserStopped?.Invoke());
            }
            catch (Exception ex)
            {
                Log.Error($"执行 {label}", ex);
                _status.SetIdle($"{label} 出错：{ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts.Dispose();
                    _cts = null;
                    Ui.OnUi(() =>
                    {
                        CurrentLabel = null;
                        CurrentDuration = null;
                        RunningChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        });

        _status.SetRunning($"{label} 运行中…");
        return true;
    }
}
