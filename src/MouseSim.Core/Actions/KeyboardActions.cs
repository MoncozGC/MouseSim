using MouseSim.Core.Input;

namespace MouseSim.Core.Actions;

/// <summary>按下组合键（如 Ctrl+S）。</summary>
public class KeyPressAction : IAction
{
    public KeyModifiers Modifiers { get; set; }
    public int VirtualKey { get; set; }

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.PressKey(Modifiers, VirtualKey);
        return Task.CompletedTask;
    }

    public string Describe()
    {
        var mods = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Control)) mods.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) mods.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) mods.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Win)) mods.Add("Win");
        mods.Add(KeyNames.Get(VirtualKey));
        return "按键 " + string.Join("+", mods);
    }
}

/// <summary>按住某个键一段时间。</summary>
public class KeyHoldAction : IAction
{
    public int VirtualKey { get; set; }
    public int DurationMs { get; set; } = 1000;

    public async Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.KeyDown(VirtualKey);
        try
        {
            await Task.Delay(Math.Max(DurationMs, 0), cancellationToken);
        }
        finally
        {
            simulator.KeyUp(VirtualKey);
        }
    }

    public string Describe() => $"按住 {KeyNames.Get(VirtualKey)} {DurationMs} 毫秒";
}

/// <summary>输入一段文本（Unicode 方式发送，支持中文，不依赖键盘布局）。</summary>
public class TextInputAction : IAction
{
    public string Text { get; set; } = "";

    public Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        simulator.TypeText(Text);
        return Task.CompletedTask;
    }

    public string Describe()
    {
        var t = Text.Replace("\r", "").Replace("\n", " ");
        if (t.Length > 20) t = t[..20] + "…";
        return $"输入文本：{t}";
    }
}
