using MouseSim.Core.Actions;
using MouseSim.Core.Models;

namespace MouseSim.Core.Engine;

/// <summary>
/// 回放速度缩放：按脚本的速度倍率生成一份缩放后的脚本副本。
/// ×2 表示快一倍：所有等待、每步延迟、拖拽时长、按住时长除以 2；
/// 点击/移动/滚轮/按键本身瞬时完成，不受影响。倍率为 1 时原样返回（零拷贝）。
/// </summary>
public static class PlaybackScaler
{
    public static Script Scale(Script script)
    {
        var speed = script.SpeedMultiplier;
        if (speed is 1.0 || speed <= 0) return script;

        double inv = 1.0 / speed;
        return new Script
        {
            Name = script.Name,
            LoopCount = script.LoopCount,
            StepDelayMs = Math.Max(0, (int)(script.StepDelayMs * inv)),
            RepeatMinutes = script.RepeatMinutes,
            SpeedMultiplier = speed,
            Actions = script.Actions.Select(a => ScaleAction(a, inv)).ToList(),
        };
    }

    private static IAction ScaleAction(IAction action, double inv) => action switch
    {
        DelayAction d => new DelayAction { Milliseconds = Math.Max(0, (int)(d.Milliseconds * inv)) },
        MouseDragAction m => new MouseDragAction
        {
            FromX = m.FromX, FromY = m.FromY, ToX = m.ToX, ToY = m.ToY,
            Button = m.Button,
            DurationMs = Math.Max(50, (int)(m.DurationMs * inv)),
        },
        KeyHoldAction k => new KeyHoldAction
        {
            VirtualKey = k.VirtualKey,
            DurationMs = Math.Max(0, (int)(k.DurationMs * inv)),
        },
        _ => action,
    };
}
