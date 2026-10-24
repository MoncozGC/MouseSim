using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.Core.Actions;
using MouseSim.Core.Input;

using MouseButton = MouseSim.Core.Input.MouseButton;

namespace MouseSim.App.ViewModels;

public sealed record ActionTypeInfo(string Key, string Label);

/// <summary>动作编辑对话框的数据：一个 VM 承载所有类型字段的超集，
/// 按当前类型显示对应分区，确定时校验并构建动作对象。</summary>
public class ActionEditViewModel : ObservableObject
{
    // 注意：WPF 的 {Binding} 只能绑定实例属性，静态字段绑不上（曾导致下拉框为空），
    // 因此这里用静态数据 + 实例属性暴露的方式。
    private static readonly IReadOnlyList<ActionTypeInfo> AllTypes =
    [
        new("click", "鼠标点击"),
        new("move", "鼠标移动"),
        new("drag", "鼠标拖拽"),
        new("scroll", "滚轮"),
        new("keypress", "按键 / 组合键"),
        new("keyhold", "按住按键"),
        new("text", "输入文本"),
        new("delay", "等待"),
    ];

    public IReadOnlyList<ActionTypeInfo> TypeOptions => AllTypes;

    public IReadOnlyList<EnumOption<MouseButton>> ButtonOptions { get; } =
    [
        new(MouseButton.Left, "左键"),
        new(MouseButton.Right, "右键"),
        new(MouseButton.Middle, "中键"),
    ];

    public ActionEditViewModel(IAction action)
    {
        _selectedType = TypeOptions[0];
        LoadFrom(action);
    }

    private ActionTypeInfo _selectedType = null!;
    public ActionTypeInfo SelectedType
    {
        get => _selectedType;
        set
        {
            if (!SetProperty(ref _selectedType, value)) return;
            if (value.Key == "drag" && string.IsNullOrWhiteSpace(FromX) && string.IsNullOrWhiteSpace(FromY))
            {
                FromX = X;
                FromY = Y;
                ToX = ToX ?? "";
                ToY = ToY ?? "";
            }
            NotifySectionVisibility();
        }
    }

    // —— 坐标（点击/移动/拖拽共用）——
    public string X { get; set; } = "0";
    public string Y { get; set; } = "0";
    public string FromX { get; set; } = "0";
    public string FromY { get; set; } = "0";
    public string ToX { get; set; } = "0";
    public string ToY { get; set; } = "0";

    // —— 点击 ——
    private bool _useCurrentPosition;
    public bool UseCurrentPosition { get => _useCurrentPosition; set => SetProperty(ref _useCurrentPosition, value); }
    public bool UseCoordinates => !UseCurrentPosition;
    private bool _doubleClick;
    public bool DoubleClick { get => _doubleClick; set => SetProperty(ref _doubleClick, value); }
    private EnumOption<MouseButton> _button = null!;
    public EnumOption<MouseButton> Button { get => _button; set => SetProperty(ref _button, value); }

    // —— 拖拽 ——
    public string DragDuration { get; set; } = "300";

    // —— 滚轮 ——
    public string ScrollDelta { get; set; } = "-3";
    private bool _scrollHorizontal;
    public bool ScrollHorizontal { get => _scrollHorizontal; set => SetProperty(ref _scrollHorizontal, value); }

    // —— 按键 ——
    private bool _hasCtrl, _hasAlt, _hasShift, _hasWin;
    public bool HasCtrl { get => _hasCtrl; set => SetProperty(ref _hasCtrl, value); }
    public bool HasAlt { get => _hasAlt; set => SetProperty(ref _hasAlt, value); }
    public bool HasShift { get => _hasShift; set => SetProperty(ref _hasShift, value); }
    public bool HasWin { get => _hasWin; set => SetProperty(ref _hasWin, value); }
    private Key? _pressKey;
    public Key? PressKey { get => _pressKey; set => SetProperty(ref _pressKey, value); }

    // —— 按住 ——
    private Key? _holdKey;
    public Key? HoldKey { get => _holdKey; set => SetProperty(ref _holdKey, value); }
    public string HoldDuration { get; set; } = "1000";

    // —— 文本 ——
    public string Text { get; set; } = "";

    // —— 延迟 ——
    public string DelayMs { get; set; } = "500";

    // —— 分区可见性 ——
    public bool ShowMove => SelectedType.Key == "move";
    public bool ShowClick => SelectedType.Key == "click";
    public bool ShowDrag => SelectedType.Key == "drag";
    public bool ShowScroll => SelectedType.Key == "scroll";
    public bool ShowKeyPress => SelectedType.Key == "keypress";
    public bool ShowKeyHold => SelectedType.Key == "keyhold";
    public bool ShowText => SelectedType.Key == "text";
    public bool ShowDelay => SelectedType.Key == "delay";

    public string ErrorText { get; private set; } = "";

    private void NotifySectionVisibility()
    {
        OnPropertyChanged(nameof(ShowMove));
        OnPropertyChanged(nameof(ShowClick));
        OnPropertyChanged(nameof(ShowDrag));
        OnPropertyChanged(nameof(ShowScroll));
        OnPropertyChanged(nameof(ShowKeyPress));
        OnPropertyChanged(nameof(ShowKeyHold));
        OnPropertyChanged(nameof(ShowText));
        OnPropertyChanged(nameof(ShowDelay));
    }

    private void LoadFrom(IAction action)
    {
        switch (action)
        {
            case MouseClickAction a:
                SelectedType = TypeOptions[0];
                Button = ButtonOptions[(int)a.Button];
                UseCurrentPosition = a.UseCurrentPosition;
                X = a.X.ToString();
                Y = a.Y.ToString();
                DoubleClick = a.DoubleClick;
                break;
            case MouseMoveAction a:
                SelectedType = TypeOptions[1];
                X = a.X.ToString();
                Y = a.Y.ToString();
                Button = ButtonOptions[0];
                break;
            case MouseDragAction a:
                SelectedType = TypeOptions[2];
                FromX = a.FromX.ToString();
                FromY = a.FromY.ToString();
                ToX = a.ToX.ToString();
                ToY = a.ToY.ToString();
                Button = ButtonOptions[(int)a.Button];
                DragDuration = a.DurationMs.ToString();
                break;
            case MouseScrollAction a:
                SelectedType = TypeOptions[3];
                ScrollDelta = a.Delta.ToString();
                ScrollHorizontal = a.Horizontal;
                Button = ButtonOptions[0];
                break;
            case KeyPressAction a:
                SelectedType = TypeOptions[4];
                HasCtrl = a.Modifiers.HasFlag(KeyModifiers.Control);
                HasAlt = a.Modifiers.HasFlag(KeyModifiers.Alt);
                HasShift = a.Modifiers.HasFlag(KeyModifiers.Shift);
                HasWin = a.Modifiers.HasFlag(KeyModifiers.Win);
                PressKey = KeyInterop.KeyFromVirtualKey(a.VirtualKey);
                Button = ButtonOptions[0];
                break;
            case KeyHoldAction a:
                SelectedType = TypeOptions[5];
                HoldKey = KeyInterop.KeyFromVirtualKey(a.VirtualKey);
                HoldDuration = a.DurationMs.ToString();
                Button = ButtonOptions[0];
                break;
            case TextInputAction a:
                SelectedType = TypeOptions[6];
                Text = a.Text;
                Button = ButtonOptions[0];
                break;
            case DelayAction a:
                SelectedType = TypeOptions[7];
                DelayMs = a.Milliseconds.ToString();
                Button = ButtonOptions[0];
                break;
            default:
                throw new NotSupportedException($"不支持编辑的动作类型 {action.GetType().Name}");
        }
    }

    /// <summary>校验并构建动作；非法时设置 ErrorText 并返回 null。</summary>
    public IAction? BuildAction()
    {
        try
        {
            IAction result = SelectedType.Key switch
            {
                "click" => new MouseClickAction
                {
                    Button = Button.Value,
                    UseCurrentPosition = UseCurrentPosition,
                    X = Parse(X, "X 坐标", -100_000, 100_000),
                    Y = Parse(Y, "Y 坐标", -100_000, 100_000),
                    DoubleClick = DoubleClick,
                },
                "move" => new MouseMoveAction
                {
                    X = Parse(X, "X 坐标", -100_000, 100_000),
                    Y = Parse(Y, "Y 坐标", -100_000, 100_000),
                },
                "drag" => new MouseDragAction
                {
                    FromX = Parse(FromX, "起点 X", -100_000, 100_000),
                    FromY = Parse(FromY, "起点 Y", -100_000, 100_000),
                    ToX = Parse(ToX, "终点 X", -100_000, 100_000),
                    ToY = Parse(ToY, "终点 Y", -100_000, 100_000),
                    Button = Button.Value,
                    DurationMs = Parse(DragDuration, "拖拽时长", 50, 60_000),
                },
                "scroll" => new MouseScrollAction
                {
                    Delta = Parse(ScrollDelta, "滚动格数", -100, 100),
                    Horizontal = ScrollHorizontal,
                },
                "keypress" => new KeyPressAction
                {
                    Modifiers = BuildModifiers(),
                    VirtualKey = RequireKey(PressKey),
                },
                "keyhold" => new KeyHoldAction
                {
                    VirtualKey = RequireKey(HoldKey),
                    DurationMs = Parse(HoldDuration, "按住时长", 0, 3_600_000),
                },
                "text" => new TextInputAction { Text = Text },
                "delay" => new DelayAction { Milliseconds = Parse(DelayMs, "等待时长", 0, 3_600_000) },
                _ => throw new InvalidOperationException("未知动作类型"),
            };

            ErrorText = "";
            return result;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException)
        {
            ErrorText = ex.Message;
            OnPropertyChanged(nameof(ErrorText));
            return null;
        }
    }

    private KeyModifiers BuildModifiers()
    {
        var mods = KeyModifiers.None;
        if (HasCtrl) mods |= KeyModifiers.Control;
        if (HasAlt) mods |= KeyModifiers.Alt;
        if (HasShift) mods |= KeyModifiers.Shift;
        if (HasWin) mods |= KeyModifiers.Win;
        return mods;
    }

    private static int RequireKey(Key? key)
    {
        if (key is null or Key.None)
            throw new InvalidOperationException("请选择一个按键");
        return KeyInterop.VirtualKeyFromKey(key.Value);
    }

    private static int Parse(string? text, string field, int min, int max)
    {
        if (!int.TryParse(text?.Trim(), out var value))
            throw new FormatException($"「{field}」必须是整数");
        if (value < min || value > max)
            throw new FormatException($"「{field}」需在 {min} ~ {max} 之间");
        return value;
    }
}
