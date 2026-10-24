using System.Diagnostics;

namespace MouseSim.Core.Input;

/// <summary>
/// 基于 Win32 SendInput 的输入模拟实现。
/// 绝对定位使用 SetCursorPos（像素精确、天然支持多显示器），按键与滚轮事件走 SendInput。
/// </summary>
public sealed class SendInputSimulator : IInputSimulator
{
    // 按下与抬起之间的间隔，保证目标程序能识别为一次点击
    private const int ClickGapMs = 10;
    private const int DoubleClickGapMs = 40;

    public void MoveTo(int x, int y)
    {
        if (!Win32.SetCursorPos(x, y))
            ThrowLastError("移动鼠标");
    }

    public void MouseDown(MouseButton button) => SendMouseButton(button, down: true);

    public void MouseUp(MouseButton button) => SendMouseButton(button, down: false);

    public void Click(MouseButton button)
    {
        MouseDown(button);
        Thread.Sleep(ClickGapMs);
        MouseUp(button);
    }

    public void DoubleClick(MouseButton button)
    {
        Click(button);
        Thread.Sleep(DoubleClickGapMs);
        Click(button);
    }

    public void Drag(int fromX, int fromY, int toX, int toY, MouseButton button, int durationMs)
    {
        durationMs = Math.Max(durationMs, 50);
        MoveTo(fromX, fromY);
        Thread.Sleep(ClickGapMs);
        MouseDown(button);

        // 分段移动，让目标程序收到中间的鼠标移动消息，形成真实的拖拽轨迹
        int steps = Math.Clamp(durationMs / 15, 5, 40);
        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            MoveTo(Lerp(fromX, toX, t), Lerp(fromY, toY, t));
            int targetMs = (int)(durationMs * t);
            int remaining = targetMs - (int)sw.ElapsedMilliseconds;
            if (remaining > 0) Thread.Sleep(remaining);
        }

        Thread.Sleep(ClickGapMs);
        MouseUp(button);
    }

    public void Scroll(int delta, bool horizontal)
    {
        var input = new Win32.INPUT
        {
            Type = Win32.INPUT_MOUSE,
            U = new Win32.INPUTUNION
            {
                Mi = new Win32.MOUSEINPUT
                {
                    MouseData = unchecked((uint)(delta * 120)),
                    Flags = horizontal ? Win32.MOUSEEVENTF_HWHEEL : Win32.MOUSEEVENTF_WHEEL,
                    ExtraInfo = InputSource.OwnSendMarker,
                },
            },
        };
        Send(input, "滚轮");
    }

    public void KeyDown(int virtualKey) => SendKey(virtualKey, up: false);

    public void KeyUp(int virtualKey) => SendKey(virtualKey, up: true);

    public void PressKey(KeyModifiers modifiers, int virtualKey)
    {
        var mods = new List<int>();
        if (modifiers.HasFlag(KeyModifiers.Control)) mods.Add(0x11);
        if (modifiers.HasFlag(KeyModifiers.Alt)) mods.Add(0x12);
        if (modifiers.HasFlag(KeyModifiers.Shift)) mods.Add(0x10);
        if (modifiers.HasFlag(KeyModifiers.Win)) mods.Add(0x5B);

        foreach (var mod in mods) KeyDown(mod);
        Thread.Sleep(ClickGapMs);
        KeyDown(virtualKey);
        Thread.Sleep(ClickGapMs);
        KeyUp(virtualKey);
        for (int i = mods.Count - 1; i >= 0; i--) KeyUp(mods[i]);
    }

    public void TypeText(string text)
    {
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\r': // 与 '\n' 组成换行时跳过，由 '\n' 统一处理
                    continue;
                case '\n':
                    PressKey(KeyModifiers.None, 0x0D); // Enter
                    continue;
                case '\t':
                    PressKey(KeyModifiers.None, 0x09); // Tab
                    continue;
            }

            // 以 Unicode 事件发送，不依赖当前键盘布局，可输入任意文字
            var down = new Win32.INPUT
            {
                Type = Win32.INPUT_KEYBOARD,
                U = new Win32.INPUTUNION
                {
                    Ki = new Win32.KEYBDINPUT { Scan = ch, Flags = Win32.KEYEVENTF_UNICODE, ExtraInfo = InputSource.OwnSendMarker },
                },
            };
            var up = new Win32.INPUT
            {
                Type = Win32.INPUT_KEYBOARD,
                U = new Win32.INPUTUNION
                {
                    Ki = new Win32.KEYBDINPUT { Scan = ch, Flags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP, ExtraInfo = InputSource.OwnSendMarker },
                },
            };
            Send(new[] { down, up }, $"输入字符 {(int)ch}");
        }
    }

    private static int Lerp(int from, int to, double t) => (int)Math.Round(from + (to - from) * t);

    private static void SendMouseButton(MouseButton button, bool down)
    {
        uint flag = (button, down) switch
        {
            (MouseButton.Left, true) => Win32.MOUSEEVENTF_LEFTDOWN,
            (MouseButton.Left, false) => Win32.MOUSEEVENTF_LEFTUP,
            (MouseButton.Right, true) => Win32.MOUSEEVENTF_RIGHTDOWN,
            (MouseButton.Right, false) => Win32.MOUSEEVENTF_RIGHTUP,
            (MouseButton.Middle, true) => Win32.MOUSEEVENTF_MIDDLEDOWN,
            (MouseButton.Middle, false) => Win32.MOUSEEVENTF_MIDDLEUP,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        var input = new Win32.INPUT
        {
            Type = Win32.INPUT_MOUSE,
            U = new Win32.INPUTUNION { Mi = new Win32.MOUSEINPUT { Flags = flag, ExtraInfo = InputSource.OwnSendMarker } },
        };
        Send(input, $"鼠标按键 {button} {(down ? "按下" : "抬起")}");
    }

    private static void SendKey(int virtualKey, bool up)
    {
        var input = new Win32.INPUT
        {
            Type = Win32.INPUT_KEYBOARD,
            U = new Win32.INPUTUNION
            {
                Ki = new Win32.KEYBDINPUT
                {
                    Vk = unchecked((ushort)virtualKey),
                    Scan = unchecked((ushort)Win32.MapVirtualKey((uint)virtualKey, Win32.MAPVK_VK_TO_VSC)),
                    Flags = up ? Win32.KEYEVENTF_KEYUP : 0,
                    ExtraInfo = InputSource.OwnSendMarker,
                },
            },
        };
        Send(input, $"键盘 {KeyNames.Get(virtualKey)} {(up ? "抬起" : "按下")}");
    }

    private static void Send(Win32.INPUT input, string operation) => Send(new[] { input }, operation);

    private static void Send(Win32.INPUT[] inputs, string operation)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException($"发送输入事件失败（{operation}），实际发送 {sent}/{inputs.Length}");
    }

    private static void ThrowLastError(string operation)
        => throw new InvalidOperationException($"{operation}失败，Win32 错误码 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
}
