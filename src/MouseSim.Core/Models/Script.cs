using MouseSim.Core.Actions;

namespace MouseSim.Core.Models;

/// <summary>
/// 一个脚本 = 动作序列 + 循环/延迟设置。
/// LoopCount = 0 表示无限循环；StepDelayMs 在每个动作执行完后附加等待；
/// RepeatMinutes &gt; 0 时按时间重复（优先于 LoopCount，到时停止）。
/// </summary>
public class Script
{
    public string Name { get; set; } = "新建脚本";
    public int LoopCount { get; set; } = 1;
    public int StepDelayMs { get; set; }
    public int RepeatMinutes { get; set; }
    public List<IAction> Actions { get; set; } = [];
}
