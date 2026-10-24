namespace MouseSim.Core.Input;

/// <summary>屏幕坐标查询。</summary>
public static class ScreenInfo
{
    /// <summary>当前鼠标光标位置（物理像素坐标）。</summary>
    public static (int X, int Y) CursorPosition
    {
        get
        {
            if (!Win32.GetCursorPos(out var p))
                return (0, 0);
            return (p.X, p.Y);
        }
    }

    /// <summary>指定坐标处是否是本进程的窗口（录制时忽略对本程序自身的操作）。</summary>
    public static bool IsOwnProcessWindowAt(int x, int y)
    {
        var hwnd = Win32.WindowFromPoint(new Win32.POINT { X = x, Y = y });
        if (hwnd == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }
}
