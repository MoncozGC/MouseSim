using MouseSim.Core.Actions;
using MouseSim.Core.Input;

namespace MouseSim.Core.Recording;

/// <summary>
/// 录制状态机：把低级钩子的原始事件流转换为可回放的动作序列。
/// 规则：
/// - 按下到抬起之间位移很小 → 单击；短时间内在同位置再次单击 → 合并为双击
/// - 按住期间位移较大 → 拖拽（起点=按下位置，终点=抬起位置，时长=实际耗时）
/// - 连续滚轮累计成一次滚动（位移或其它事件打断时结算）
/// - 修饰键 + 按键 → 组合键（只按修饰键不记录；自动重复只记一次）
/// - 动作之间的真实时间间隔记录为等待：小于 MinGapMs 忽略，大于 MaxGapMs 截断
/// - 只跳过本程序自己回放注入的事件（dwExtraInfo 魔数标记）；
///   其他程序注入的输入（鼠标/改键驱动的输出等）与真实输入一视同仁，全部录制
/// 事件只会从钩子线程进入，Stop 从其它线程调用，全部方法加锁。
/// </summary>
public sealed class MacroRecorder
{
    private const int DragThresholdPx = 8;
    private const int DoubleClickPx = 6;
    private const int ScrollFlushPx = 60;

    private static readonly int DoubleClickTimeMs = (int)Win32.GetDoubleClickTime();

    private readonly Func<MouseHookEvent, bool>? _ignoreMouse;
    private readonly Func<KeyHookEvent, bool>? _ignoreKey;
    private readonly int _minGapMs;
    private readonly int _maxGapMs;
    private readonly object _gate = new();

    private readonly List<IAction> _actions = [];
    private long _lastRecordedTime;
    private string _lastDescription = "";

    // 按下状态（同一时间只跟踪一个按钮，多键同按属罕见场景）
    private int _downX, _downY;
    private long _downTime;
    private MouseButton? _downButton;
    private bool _dragging;

    // 双击检测：上一次单击的信息
    private (MouseButton button, int x, int y, long time)? _lastClick;

    // 滚动累计
    private int _pendingWheel;       // 原始滚轮单位（一格 = 120）
    private int _pendingWheelX;
    private (int x, int y, long time)? _scrollStart;
    private (int x, int y)? _scrollPos;
    private long _lastScrollTime;

    // 键盘状态
    private readonly HashSet<int> _heldKeys = [];
    private readonly Dictionary<int, (KeyModifiers mods, long time)> _pendingKeys = [];
    private KeyModifiers _heldModifiers;

    public MacroRecorder(
        Func<MouseHookEvent, bool>? ignoreMouse = null,
        Func<KeyHookEvent, bool>? ignoreKey = null,
        int minGapMs = 60,
        int maxGapMs = 60_000)
    {
        _ignoreMouse = ignoreMouse;
        _ignoreKey = ignoreKey;
        _minGapMs = minGapMs;
        _maxGapMs = maxGapMs;
    }

    /// <summary>已录制的动作数（含等待）。</summary>
    public int RecordedCount
    {
        get { lock (_gate) return _actions.Count; }
    }

    /// <summary>最后一个动作的描述（用于界面实时反馈）。</summary>
    public string LastDescription
    {
        get { lock (_gate) return _lastDescription; }
    }

    public void OnMouse(MouseHookEvent e)
    {
        lock (_gate)
        {
            if (e.ExtraInfo == InputSource.OwnSendMarker) return; // 本程序回放的输入不录
            if (_ignoreMouse != null && _ignoreMouse(e)) return;

            switch (e.Type)
            {
                case MouseHookEventType.Move:
                    OnMove(e);
                    break;
                case MouseHookEventType.LeftDown:
                    OnDown(MouseButton.Left, e);
                    break;
                case MouseHookEventType.RightDown:
                    OnDown(MouseButton.Right, e);
                    break;
                case MouseHookEventType.MiddleDown:
                    OnDown(MouseButton.Middle, e);
                    break;
                case MouseHookEventType.LeftUp:
                case MouseHookEventType.RightUp:
                case MouseHookEventType.MiddleUp:
                    OnUp(e);
                    break;
                case MouseHookEventType.Scroll:
                    OnScroll(e, horizontal: false);
                    break;
                case MouseHookEventType.ScrollHorizontal:
                    OnScroll(e, horizontal: true);
                    break;
            }
        }
    }

    public void OnKey(KeyHookEvent e)
    {
        lock (_gate)
        {
            if (e.ExtraInfo == InputSource.OwnSendMarker) return; // 本程序回放的输入不录
            if (_ignoreKey != null && _ignoreKey(e)) return;

            var vk = e.VirtualKey;
            var mod = ToModifier(vk);
            if (mod != KeyModifiers.None)
            {
                if (e.IsDown) _heldModifiers |= mod;
                else _heldModifiers &= ~mod;
                return;
            }

            if (e.IsDown)
            {
                // 自动重复：按住期间的重复按下只记一次
                _pendingKeys.TryAdd(vk, (_heldModifiers, e.Timestamp));
                _heldKeys.Add(vk);
            }
            else if (_pendingKeys.Remove(vk, out var info))
            {
                _heldKeys.Remove(vk);
                Append(new KeyPressAction { Modifiers = info.mods, VirtualKey = vk }, info.time, e.Timestamp);
            }
        }
    }

    /// <summary>结束录制：结算挂起的状态并返回动作序列。</summary>
    public List<IAction> Stop()
    {
        lock (_gate)
        {
            FlushScroll();
            // 按住未抬起的键、未完成的按下直接丢弃
            _pendingKeys.Clear();
            _downButton = null;
            return [.. _actions];
        }
    }

    // —— 鼠标 ——

    private void OnMove(MouseHookEvent e)
    {
        if (_downButton != null)
        {
            if (!_dragging && Distance(e.X, e.Y, _downX, _downY) > DragThresholdPx)
                _dragging = true;
        }
    }

    private void OnDown(MouseButton button, MouseHookEvent e)
    {
        FlushScroll();
        if (_downButton != null) return; // 已有按钮按住，忽略第二按钮
        _downButton = button;
        _downX = e.X;
        _downY = e.Y;
        _downTime = e.Timestamp;
        _dragging = false;
    }

    private void OnUp(MouseHookEvent e)
    {
        var button = _downButton;
        if (button == null) return;
        _downButton = null;

        if (_dragging)
        {
            _lastClick = null;
            Append(new MouseDragAction
            {
                FromX = _downX,
                FromY = _downY,
                ToX = e.X,
                ToY = e.Y,
                Button = button.Value,
                DurationMs = Math.Clamp((int)(e.Timestamp - _downTime), 50, 60_000),
            }, _downTime, e.Timestamp);
            return;
        }

        // 双击：同按钮、同位置、间隔不超过系统双击时间（坐标取按下位置）
        if (_lastClick is { } last
            && last.button == button
            && last.time > 0
            && e.Timestamp - last.time <= DoubleClickTimeMs
            && Distance(_downX, _downY, last.x, last.y) <= DoubleClickPx
            && TryUpgradeLastClickToDouble())
        {
            _lastClick = null;
            return;
        }

        Append(new MouseClickAction { Button = button.Value, X = _downX, Y = _downY }, _downTime, e.Timestamp);
        _lastClick = (button.Value, _downX, _downY, e.Timestamp);
    }

    /// <summary>把已记录的最后一次单击升级为双击（顺带移除两次单击之间误记的等待）。</summary>
    private bool TryUpgradeLastClickToDouble()
    {
        var removedDelays = new Stack<DelayAction>();
        while (_actions.Count > 0 && _actions[^1] is DelayAction d)
        {
            _actions.RemoveAt(_actions.Count - 1);
            removedDelays.Push(d);
        }

        if (_actions.Count > 0 && _actions[^1] is MouseClickAction click && !click.DoubleClick)
        {
            click.DoubleClick = true;
            _lastDescription = click.Describe();
            return true;
        }

        // 恢复（理论上不会走到）
        while (removedDelays.Count > 0)
            _actions.Add(removedDelays.Pop());
        return false;
    }

    private void OnScroll(MouseHookEvent e, bool horizontal)
    {
        // 位置明显变化 → 先结算上一段
        if (_scrollPos is { } p && Distance(e.X, e.Y, p.x, p.y) > ScrollFlushPx)
            FlushScroll();

        if (horizontal) _pendingWheelX += e.WheelDelta;
        else _pendingWheel += e.WheelDelta;
        _scrollStart ??= (e.X, e.Y, e.Timestamp);
        _scrollPos = (e.X, e.Y);
        _lastScrollTime = e.Timestamp;
    }

    private void FlushScroll()
    {
        if (_pendingWheel != 0 && _scrollStart is { } s)
            Append(new MouseScrollAction { Delta = ToNotches(_pendingWheel), Horizontal = false }, s.time, _lastScrollTime);
        if (_pendingWheelX != 0 && _scrollStart is { } sx)
            Append(new MouseScrollAction { Delta = ToNotches(_pendingWheelX), Horizontal = true }, sx.time, _lastScrollTime);

        _pendingWheel = 0;
        _pendingWheelX = 0;
        _scrollStart = null;
        _scrollPos = null;
    }

    /// <summary>原始滚轮单位换算为“格”（一格 120；触控板小步滚动凑不满一格时按方向记一格）。</summary>
    private static int ToNotches(int wheelUnits)
    {
        if (wheelUnits == 0) return 0;
        var notches = (int)Math.Round(wheelUnits / 120.0, MidpointRounding.AwayFromZero);
        return notches == 0 ? Math.Sign(wheelUnits) : notches;
    }

    // —— 通用 ——

    /// <summary>追加动作：先用与上一动作完成时刻的间隔补一个等待（达到阈值才记），
    /// 再记动作本身，并把“完成时刻”推进为本动作的结束时间。</summary>
    private void Append(IAction action, long startTime, long endTime)
    {
        if (_actions.Count > 0)
        {
            var gap = (int)(startTime - _lastRecordedTime);
            if (gap >= _minGapMs)
                _actions.Add(new DelayAction { Milliseconds = Math.Min(gap, _maxGapMs) });
        }

        _actions.Add(action);
        _lastRecordedTime = endTime;
        _lastDescription = action.Describe();
    }

    private static double Distance(int x1, int y1, int x2, int y2)
        => Math.Sqrt((x1 - x2) * (double)(x1 - x2) + (y1 - y2) * (double)(y1 - y2));

    private static KeyModifiers ToModifier(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => KeyModifiers.Shift,   // Shift / 左Shift / 右Shift
        0x11 or 0xA2 or 0xA3 => KeyModifiers.Control, // Ctrl / 左Ctrl / 右Ctrl
        0x12 or 0xA4 or 0xA5 => KeyModifiers.Alt,     // Alt / 左Alt / 右Alt
        0x5B or 0x5C => KeyModifiers.Win,             // 左Win / 右Win
        _ => KeyModifiers.None,
    };
}
