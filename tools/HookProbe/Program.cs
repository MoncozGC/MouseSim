using System.Runtime.InteropServices;
using MouseSim.Core.Input;
using MouseSim.Core.Recording;

// 端到端链路探针：GlobalInputHook → MacroRecorder（与 App 相同的接线方式）。
// 用不带标记的 SendInput 模拟“真实/第三方输入”（应被录制），
// 再用带 OwnSendMarker 的 SendInputSimulator 模拟“本程序回放”（不应被录制）。

internal static class Raw
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public IntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort Vk; public ushort Scan; public uint Flags; public uint Time; public IntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT Mi; [FieldOffset(0)] public KEYBDINPUT Ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint Type; public INPUTUNION U; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}

public static class Program
{
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static async Task Main()
    {
        using var hook = new GlobalInputHook();
        var recorder = new MacroRecorder();
        hook.Mouse += recorder.OnMouse;
        hook.Keyboard += recorder.OnKey;
        await hook.InstallAsync();
        Console.WriteLine("hooks installed");

        Console.WriteLine("-- 阶段1：模拟真实/第三方输入（无标记 SendInput）--");
        SendRawMouse(MOUSEEVENTF_MOVE, 8, 8);
        await Task.Delay(120);
        SendRawMouse(MOUSEEVENTF_MIDDLEDOWN, 0, 0);
        await Task.Delay(40);
        SendRawMouse(MOUSEEVENTF_MIDDLEUP, 0, 0);
        await Task.Delay(120);
        SendRawKey(0x7C, down: true); // F13
        await Task.Delay(40);
        SendRawKey(0x7C, down: false);
        await Task.Delay(600);

        Console.WriteLine("-- 阶段2：本程序回放（带 OwnSendMarker，不应被录制）--");
        var sim = new SendInputSimulator();
        sim.Click(MouseButton.Middle);
        sim.PressKey(KeyModifiers.None, 0x7C);
        await Task.Delay(600);

        var actions = recorder.Stop();
        await hook.UninstallAsync();

        Console.WriteLine($"recorded actions: {actions.Count}");
        foreach (var a in actions)
            Console.WriteLine($"  - {a.GetType().Name}: {a.Describe()}");

        bool pass = actions.Count == 3; // 中键点击 + 间隔等待 + F13 按键，回放的两个事件不应出现
        Console.WriteLine(pass ? "RESULT: PASS — 录制链路正常，且回放自注入被正确排除"
                               : "RESULT: FAIL — 录制数量不符合预期");
    }

    private static void SendRawMouse(uint flags, int dx, int dy)
    {
        var input = new Raw.INPUT
        {
            Type = 0,
            U = new Raw.INPUTUNION { Mi = new Raw.MOUSEINPUT { Dx = dx, Dy = dy, Flags = flags } },
        };
        Raw.SendInput(1, new[] { input }, Marshal.SizeOf<Raw.INPUT>());
    }

    private static void SendRawKey(int vk, bool down)
    {
        var input = new Raw.INPUT
        {
            Type = 1,
            U = new Raw.INPUTUNION { Ki = new Raw.KEYBDINPUT { Vk = unchecked((ushort)vk), Flags = down ? 0u : KEYEVENTF_KEYUP } },
        };
        Raw.SendInput(1, new[] { input }, Marshal.SizeOf<Raw.INPUT>());
    }
}
