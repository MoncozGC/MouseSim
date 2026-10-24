namespace MouseSim.App.Infrastructure;

/// <summary>
/// 热键录制协调器：开始录制新热键时，当前已注册的全局热键会拦截按键，
/// 导致按什么都录不进去。因此在进入/退出录制时通知外部临时注销/恢复全部热键。
/// </summary>
internal static class HotkeyCaptureArbiter
{
    public static event Action? CaptureStarted;
    public static event Action? CaptureEnded;

    public static void NotifyStarted() => CaptureStarted?.Invoke();
    public static void NotifyEnded() => CaptureEnded?.Invoke();
}
