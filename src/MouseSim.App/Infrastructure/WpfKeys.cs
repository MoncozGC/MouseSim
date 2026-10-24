using System.Windows.Input;

namespace MouseSim.App.Infrastructure;

/// <summary>按键选择列表与显示名转换（WPF Key 枚举 ↔ 界面文字）。</summary>
public static class WpfKeys
{
    public static readonly IReadOnlyList<Key> SelectableKeys = Build();

    public static string Display(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "小键盘" + ((int)key - (int)Key.NumPad0),
        Key.Return => "Enter",
        Key.Back => "Backspace",
        Key.Snapshot => "PrintScreen",
        Key.Prior => "PageUp",
        Key.Next => "PageDown",
        Key.OemTilde => "`",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemPipe => "\\",
        Key.Multiply => "小键盘*",
        Key.Add => "小键盘+",
        Key.Subtract => "小键盘-",
        Key.Divide => "小键盘/",
        Key.Decimal => "小键盘.",
        _ => key.ToString(),
    };

    private static List<Key> Build()
    {
        var keys = new List<Key>();

        keys.AddRange(Enumerable.Range(0, 26).Select(i => (Key)((int)Key.A + i)));
        keys.AddRange(Enumerable.Range(0, 10).Select(i => (Key)((int)Key.D0 + i)));
        keys.AddRange(Enumerable.Range(0, 24).Select(i => (Key)((int)Key.F1 + i)));
        keys.AddRange(Enumerable.Range(0, 10).Select(i => (Key)((int)Key.NumPad0 + i)));

        keys.AddRange(new[]
        {
            Key.Space, Key.Return, Key.Escape, Key.Tab, Key.Back, Key.Delete, Key.Insert,
            Key.Home, Key.End, Key.Prior, Key.Next, Key.Left, Key.Up, Key.Right, Key.Down,
            Key.Snapshot, Key.Pause, Key.CapsLock, Key.NumLock, Key.Scroll,
            Key.OemTilde, Key.OemMinus, Key.OemPlus, Key.OemOpenBrackets, Key.OemCloseBrackets,
            Key.OemSemicolon, Key.OemQuotes, Key.OemComma, Key.OemPeriod, Key.OemQuestion, Key.OemPipe,
            Key.Multiply, Key.Add, Key.Subtract, Key.Divide, Key.Decimal,
        });

        return keys;
    }
}
