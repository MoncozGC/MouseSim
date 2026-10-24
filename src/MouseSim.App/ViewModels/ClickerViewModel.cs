using System.Windows.Input;
using System.Windows.Threading;
using MouseSim.App.Infrastructure;
using MouseSim.App.Services;
using MouseSim.Core.Input;
using MouseSim.Core.Models;

using MouseButton = MouseSim.Core.Input.MouseButton;

namespace MouseSim.App.ViewModels;

/// <summary>连点器页：配置并启动自动点击。</summary>
public class ClickerViewModel : ObservableObject
{
    private readonly RunnerService _runner;
    private readonly StatusViewModel _status;
    private DispatcherTimer? _pickTimer;
    private int _pickCountdown;

    public ClickerViewModel(RunnerService runner, StatusViewModel status)
    {
        _runner = runner;
        _status = status;
        _runner.RunningChanged += OnRunningChanged;
        _runner.ClickerProgress += count => Ui.OnUi(() => ClickCount = count);

        // 热键改名后按钮文案同步刷新
        _status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.StartHotkeyText)) OnPropertyChanged(nameof(StartButtonText));
            if (e.PropertyName == nameof(StatusViewModel.StopHotkeyText)) OnPropertyChanged(nameof(StopButtonText));
        };

        ButtonOptions =
        [
            new EnumOption<MouseButton>(MouseButton.Left, "左键"),
            new EnumOption<MouseButton>(MouseButton.Right, "右键"),
            new EnumOption<MouseButton>(MouseButton.Middle, "中键"),
        ];
        _selectedButton = ButtonOptions[0];

        StartCommand = new RelayCommand(Start, () => !IsRunning);
        StopCommand = new RelayCommand(_runner.Stop, () => IsRunning);
        PickCommand = new RelayCommand(TogglePick, () => !IsRunning);
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PickCommand { get; }

    public IReadOnlyList<EnumOption<MouseButton>> ButtonOptions { get; }

    public bool IsRunning => _runner.IsRunning;

    public string StartButtonText => $"开始（{_status.StartHotkeyText}）";
    public string StopButtonText => $"停止（{_status.StopHotkeyText}）";

    private string _intervalText = "100";
    public string IntervalText { get => _intervalText; set => SetProperty(ref _intervalText, value); }

    private EnumOption<MouseButton> _selectedButton;
    public EnumOption<MouseButton> SelectedButton { get => _selectedButton; set => SetProperty(ref _selectedButton, value); }

    private bool _doubleClick;
    public bool DoubleClick { get => _doubleClick; set => SetProperty(ref _doubleClick, value); }

    private bool _useFixed;
    public bool UseFixed { get => _useFixed; set => SetProperty(ref _useFixed, value); }

    private string _xText = "0";
    public string XText { get => _xText; set => SetProperty(ref _xText, value); }

    private string _yText = "0";
    public string YText { get => _yText; set => SetProperty(ref _yText, value); }

    private bool _infinite = true;
    public bool Infinite { get => _infinite; set => SetProperty(ref _infinite, value); }

    private string _repeatText = "10";
    public string RepeatText { get => _repeatText; set => SetProperty(ref _repeatText, value); }

    private int _clickCount;
    public int ClickCount { get => _clickCount; set => SetProperty(ref _clickCount, value); }

    private string _pickButtonText = "拾取坐标";
    public string PickButtonText { get => _pickButtonText; set => SetProperty(ref _pickButtonText, value); }

    public void Start()
    {
        if (!TryParseInt(IntervalText, 10, 3_600_000, out var interval))
        {
            SetStatus("点击间隔无效：请输入 10 到 3600000 之间的毫秒数");
            return;
        }

        int repeat = 0;
        if (!Infinite && !TryParseInt(RepeatText, 1, 100_000_000, out repeat))
        {
            SetStatus("点击次数无效：请输入不小于 1 的整数，或勾选「无限」");
            return;
        }

        int x = 0, y = 0;
        if (UseFixed && (!TryParseInt(XText, -100_000, 100_000, out x) || !TryParseInt(YText, -100_000, 100_000, out y)))
        {
            SetStatus("固定坐标无效：请输入整数（可用「拾取坐标」自动获取）");
            return;
        }

        var config = new ClickerConfig
        {
            IntervalMs = interval,
            Button = SelectedButton.Value,
            PositionMode = UseFixed ? ClickerPositionMode.Fixed : ClickerPositionMode.CurrentCursor,
            X = x,
            Y = y,
            DoubleClick = DoubleClick,
            RepeatCount = Infinite ? 0 : repeat,
        };

        if (_runner.Start(config))
            ClickCount = 0;
    }

    /// <summary>拾取坐标：点击后开始 5 秒倒计时，把鼠标移到目标位置即可自动捕获。</summary>
    private void TogglePick()
    {
        if (_pickTimer != null)
        {
            StopPick();
            return;
        }

        _pickCountdown = 5;
        PickButtonText = $"5 秒后捕获，再点取消";
        _pickTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
        _pickTimer.Tick += OnPickTick;
        _pickTimer.Start();
    }

    private void OnPickTick(object? sender, EventArgs e)
    {
        _pickCountdown--;
        if (_pickCountdown > 0)
        {
            PickButtonText = $"{_pickCountdown} 秒后捕获，再点取消";
            return;
        }

        StopPick();
        var (x, y) = ScreenInfo.CursorPosition;
        XText = x.ToString();
        YText = y.ToString();
        UseFixed = true;
        SetStatus($"已捕获坐标 ({x}, {y})，请先把鼠标移回本窗口再继续操作");
    }

    private void StopPick()
    {
        if (_pickTimer == null) return;
        _pickTimer.Stop();
        _pickTimer.Tick -= OnPickTick;
        _pickTimer = null;
        PickButtonText = "拾取坐标";
    }

    private void OnRunningChanged(object? sender, EventArgs e)
    {
        Ui.OnUi(() =>
        {
            OnPropertyChanged(nameof(IsRunning));
            StopPick();
        });
    }

    private void SetStatus(string text) => _runner.ReportStatus(text);

    private static bool TryParseInt(string? text, int min, int max, out int value)
    {
        if (int.TryParse(text?.Trim(), out value) && value >= min && value <= max)
            return true;
        value = 0;
        return false;
    }
}
