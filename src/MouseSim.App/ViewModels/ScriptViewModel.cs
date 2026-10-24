using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.App.Services;
using MouseSim.App.Views;
using MouseSim.Core.Actions;
using MouseSim.Core.Input;
using MouseSim.Core.Models;
using MouseSim.Core.Persistence;

namespace MouseSim.App.ViewModels;

/// <summary>脚本列表中的一项。FilePath 可变：重命名/保存后文件路径会变化；
/// IsChecked 用于批量运行勾选。</summary>
public sealed class ScriptListItem(Script script, string filePath) : ObservableObject
{
    private bool _isChecked;

    public Script Script { get; } = script;
    public string FilePath { get; set; } = filePath;
    public string DisplayName => Script.Name;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}

/// <summary>动作列表中的一项（只读展示）。</summary>
public sealed record ActionListItem(int Index, string TypeLabel, string Description);

public class ScriptViewModel : ObservableObject
{
    private readonly AppDataStore _store;
    private readonly RunnerService _runner;
    private readonly StatusViewModel _status;

    public ScriptViewModel(AppDataStore store, RunnerService runner, StatusViewModel status)
    {
        _store = store;
        _runner = runner;
        _status = status;
        _selectedRepeatUnit = DurationUnits[0];
        _selectedBatchRepeatUnit = DurationUnits[0];

        // 热键改名后运行/停止按钮文案同步刷新
        _status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.StartHotkeyText)) OnPropertyChanged(nameof(RunButtonText));
            if (e.PropertyName == nameof(StatusViewModel.StopHotkeyText)) OnPropertyChanged(nameof(StopButtonText));
        };
        _runner.RunningChanged += (_, _) => Ui.OnUi(() =>
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsEditingEnabled));
            CommandManager.InvalidateRequerySuggested();
        });
        _runner.ScriptProgress += (loop, index, action) =>
            Ui.OnUi(() => RunStatusText = $"第 {loop + 1} 轮 · 第 {index + 1} 步：{action.Describe()}");
        _runner.BatchStage += (batchLoop, scriptIndex, script) =>
            Ui.OnUi(() => RunStatusText = $"批量第 {batchLoop + 1} 轮 · 第 {scriptIndex + 1} 个脚本：{script.Name}");

        NewCommand = new RelayCommand(NewScript, () => IsEditingEnabled);
        DuplicateCommand = new RelayCommand(DuplicateSelected, () => IsEditingEnabled && HasSelection);
        RenameCommand = new RelayCommand(RenameSelected, () => IsEditingEnabled && HasSelection);
        DeleteCommand = new RelayCommand(DeleteSelectedOrChecked, () => IsEditingEnabled && (HasSelection || HasCheckedScripts));
        SelectAllCommand = new RelayCommand(() => SetAllChecked(true), () => Scripts.Count > 0);
        ClearChecksCommand = new RelayCommand(() => SetAllChecked(false), () => HasCheckedScripts);
        RunCommand = new RelayCommand(RunSelected, () => !IsRunning && HasSelection);
        StopCommand = new RelayCommand(_runner.Stop, () => IsRunning);
        BatchRunCommand = new RelayCommand(RunBatch, () => !IsRunning && HasCheckedScripts);
        AddActionCommand = new RelayCommand<string>(AddAction!, kind => IsEditingEnabled && HasSelection);
        EditActionCommand = new RelayCommand(EditAction, () => IsEditingEnabled && HasActionSelection);
        DeleteActionCommand = new RelayCommand(DeleteAction, () => IsEditingEnabled && HasActionSelection);
        MoveUpCommand = new RelayCommand(() => MoveAction(-1), () => IsEditingEnabled && CanMove(-1));
        MoveDownCommand = new RelayCommand(() => MoveAction(1), () => IsEditingEnabled && CanMove(1));
    }

    public ObservableCollection<ScriptListItem> Scripts { get; } = [];
    public ObservableCollection<ActionListItem> CurrentActions { get; } = [];

    public ICommand NewCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearChecksCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BatchRunCommand { get; }
    public ICommand AddActionCommand { get; }
    public ICommand EditActionCommand { get; }
    public ICommand DeleteActionCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public bool IsRunning => _runner.IsRunning;
    public bool IsEditingEnabled => !IsRunning;

    public string RunButtonText => $"运行（{_status.StartHotkeyText}）";
    public string StopButtonText => $"停止（{_status.StopHotkeyText}）";

    private ScriptListItem? _selected;
    public ScriptListItem? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                LoadSelectedDetails();
            }
        }
    }

    public bool HasSelection => _selected != null;

    private int _selectedActionIndex = -1;
    public int SelectedActionIndex
    {
        get => _selectedActionIndex;
        set
        {
            if (SetProperty(ref _selectedActionIndex, value))
                OnPropertyChanged(nameof(HasActionSelection));
        }
    }

    public bool HasActionSelection => _selected != null && SelectedActionIndex >= 0 && SelectedActionIndex < CurrentActions.Count;

    private string _loopText = "1";
    public string LoopText { get => _loopText; set => SetProperty(ref _loopText, value); }

    private string _stepDelayText = "0";
    public string StepDelayText { get => _stepDelayText; set => SetProperty(ref _stepDelayText, value); }

    private string _runStatusText = "";
    public string RunStatusText { get => _runStatusText; set => SetProperty(ref _runStatusText, value); }

    private string _batchLoopText = "1";
    public string BatchLoopText { get => _batchLoopText; set => SetProperty(ref _batchLoopText, value); }

    /// <summary>是否勾选了至少一个脚本（批量运行可用）。</summary>
    public bool HasCheckedScripts => Scripts.Any(s => s.IsChecked);

    // —— 重复方式：按次数 或 按时长，二选一 ——
    private bool _useDurationRepeat;
    public bool UseDurationRepeat { get => _useDurationRepeat; set => SetProperty(ref _useDurationRepeat, value); }

    private bool _batchUseDurationRepeat;
    public bool BatchUseDurationRepeat { get => _batchUseDurationRepeat; set => SetProperty(ref _batchUseDurationRepeat, value); }

    // —— 重复时长（分钟/小时，乘数作为选项值）——
    public IReadOnlyList<OptionItem<int>> DurationUnits { get; } =
    [
        new OptionItem<int>(1, "分钟"),
        new OptionItem<int>(60, "小时"),
    ];

    private string _repeatValueText = "0";
    public string RepeatValueText { get => _repeatValueText; set => SetProperty(ref _repeatValueText, value); }

    private OptionItem<int> _selectedRepeatUnit = null!;
    public OptionItem<int> SelectedRepeatUnit { get => _selectedRepeatUnit; set => SetProperty(ref _selectedRepeatUnit, value); }

    private string _batchRepeatValueText = "0";
    public string BatchRepeatValueText { get => _batchRepeatValueText; set => SetProperty(ref _batchRepeatValueText, value); }

    private OptionItem<int> _selectedBatchRepeatUnit = null!;
    public OptionItem<int> SelectedBatchRepeatUnit { get => _selectedBatchRepeatUnit; set => SetProperty(ref _selectedBatchRepeatUnit, value); }

    /// <summary>启动时加载磁盘上的全部脚本并选中第一个。</summary>
    public void Load()
    {
        foreach (var item in Scripts)
            item.PropertyChanged -= OnItemChecked;
        Scripts.Clear();
        foreach (var file in _store.LoadScripts())
        {
            var item = new ScriptListItem(file.Script, file.FilePath);
            item.PropertyChanged += OnItemChecked;
            Scripts.Add(item);
        }
        Selected = Scripts.FirstOrDefault();
    }

    private void OnItemChecked(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScriptListItem.IsChecked))
        {
            OnPropertyChanged(nameof(HasCheckedScripts));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>加入列表并选中（统一挂上勾选状态通知）。</summary>
    private ScriptListItem AddListItem(Script script, string filePath)
    {
        var item = new ScriptListItem(script, filePath);
        item.PropertyChanged += OnItemChecked;
        Scripts.Add(item);
        return item;
    }

    /// <summary>批量运行：按列表顺序执行勾选的脚本，整批循环 BatchLoopText 次（0=无限）。
    /// 每个脚本各自的循环次数与每步延迟在其脚本内生效。</summary>
    public void RunBatch()
    {
        CommitLoopSettings();
        var checkedScripts = Scripts.Where(s => s.IsChecked).Select(s => s.Script).ToList();
        if (checkedScripts.Count == 0)
        {
            _runner.ReportStatus("请先在左侧勾选要批量运行的脚本");
            return;
        }
        if (!int.TryParse(BatchLoopText.Trim(), out var loops) || loops < 0)
        {
            _runner.ReportStatus("整批重复次数无效：0 表示无限，否则为正整数");
            return;
        }
        if (!TryParseDuration(BatchRepeatValueText, SelectedBatchRepeatUnit, out var batchDuration))
        {
            _runner.ReportStatus("批量重复时长无效：应为不小于 0 的数字");
            return;
        }

        // 二选一：勾了“按时长”就按时间跑，否则按次数
        var effectiveDuration = BatchUseDurationRepeat ? batchDuration : null;
        RunStatusText = effectiveDuration is { } d
            ? $"批量开始：{checkedScripts.Count} 个脚本，重复 {FormatMinutes((int)d.TotalMinutes)}"
            : $"批量开始：{checkedScripts.Count} 个脚本 × {(loops == 0 ? "无限" : loops)} 轮";
        _runner.StartBatch(checkedScripts, loops, effectiveDuration);
    }

    /// <summary>保存一个新脚本（如录制产物），保存后选中它。</summary>
    public async Task AddScriptAsync(Script script)
    {
        var file = await _store.SaveScriptAsync(script);
        Load();
        Selected = Scripts.FirstOrDefault(s => s.FilePath == file.FilePath) ?? Scripts.LastOrDefault();
    }

    public void RunSelected()
    {
        if (_selected == null)
        {
            _runner.ReportStatus("请先在左侧选择一个脚本");
            return;
        }
        if (!CommitLoopSettings()) return;

        if (_selected.Script.RepeatMinutes > 0)
        {
            RunStatusText = $"开始执行，重复 {FormatMinutes(_selected.Script.RepeatMinutes)} 后自动停止…";
            _runner.Start(_selected.Script, TimeSpan.FromMinutes(_selected.Script.RepeatMinutes));
        }
        else
        {
            RunStatusText = "开始执行…";
            _runner.Start(_selected.Script);
        }
    }

    private void LoadSelectedDetails()
    {
        if (_selected == null)
        {
            CurrentActions.Clear();
            LoopText = "1";
            StepDelayText = "0";
            RepeatValueText = "30";
            UseDurationRepeat = false;
            return;
        }

        LoopText = _selected.Script.LoopCount.ToString();
        StepDelayText = _selected.Script.StepDelayMs.ToString();
        RepeatValueText = _selected.Script.RepeatMinutes > 0 ? _selected.Script.RepeatMinutes.ToString() : "30";
        UseDurationRepeat = _selected.Script.RepeatMinutes > 0;
        RebuildActions();
    }

    private void RebuildActions()
    {
        CurrentActions.Clear();
        if (_selected == null) return;
        for (int i = 0; i < _selected.Script.Actions.Count; i++)
        {
            var action = _selected.Script.Actions[i];
            CurrentActions.Add(new ActionListItem(i + 1, ActionTypeLabels.Of(action), action.Describe()));
        }
        SelectedActionIndex = CurrentActions.Count > 0 ? 0 : -1;
    }

    /// <summary>把重复方式（次数/时长二选一）与每步延迟输入框写回脚本。合法时返回 true。</summary>
    private bool CommitLoopSettings()
    {
        if (_selected == null) return false;

        if (!int.TryParse(StepDelayText.Trim(), out var delay) || delay < 0)
        {
            _runner.ReportStatus("每步延迟无效：应为不小于 0 的毫秒数");
            return false;
        }

        if (UseDurationRepeat)
        {
            if (!TryParseDuration(RepeatValueText, SelectedRepeatUnit, out var duration) || duration is not { } limit)
            {
                _runner.ReportStatus("重复时长无效：请输入大于 0 的数字（分钟/小时）");
                return false;
            }
            _selected.Script.RepeatMinutes = (int)limit.TotalMinutes;
        }
        else
        {
            if (!int.TryParse(LoopText.Trim(), out var loop) || loop < 0)
            {
                _runner.ReportStatus("重复次数无效：0 表示无限，否则为正整数");
                return false;
            }
            _selected.Script.LoopCount = loop;
            _selected.Script.RepeatMinutes = 0;
        }

        _selected.Script.StepDelayMs = delay;
        return true;
    }

    private async void SaveSelected()
    {
        if (_selected == null) return;
        if (!CommitLoopSettings()) return;
        try
        {
            var file = await _store.SaveScriptAsync(_selected.Script);
            _selected.FilePath = file.FilePath;
        }
        catch (Exception ex)
        {
            Log.Error("保存脚本", ex);
            _runner.ReportStatus("保存脚本失败：" + ex.Message);
        }
    }

    private void NewScript()
    {
        var name = InputDialog.Show("新建脚本", "脚本名称：", UniqueName("新建脚本"));
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        var script = new Script { Name = name };
        try
        {
            var file = _store.SaveScriptAsync(script).GetAwaiter().GetResult();
            Selected = AddListItem(file.Script, file.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("新建脚本", ex);
            _runner.ReportStatus("新建脚本失败：" + ex.Message);
        }
    }

    private void DuplicateSelected()
    {
        if (_selected == null) return;
        var copy = JsonSerializer.Deserialize<Script>(JsonSerializer.Serialize(_selected.Script, AppDataStore.JsonOpts), AppDataStore.JsonOpts);
        if (copy == null) return;
        copy.Name = UniqueName(_selected.Script.Name + " 副本");
        try
        {
            var file = _store.SaveScriptAsync(copy).GetAwaiter().GetResult();
            Selected = AddListItem(file.Script, file.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("复制脚本", ex);
            _runner.ReportStatus("复制脚本失败：" + ex.Message);
        }
    }

    private void RenameSelected()
    {
        if (_selected == null) return;
        var name = InputDialog.Show("重命名脚本", "脚本名称：", _selected.Script.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == _selected.Script.Name) return;
        name = name.Trim();

        try
        {
            var oldPath = _selected.FilePath;
            _selected.Script.Name = name;
            var file = _store.SaveScriptAsync(_selected.Script, oldPath).GetAwaiter().GetResult();
            Load();
        }
        catch (Exception ex)
        {
            Log.Error("重命名脚本", ex);
            _runner.ReportStatus("重命名失败：" + ex.Message);
        }
    }

    /// <summary>解析重复时长输入：数值 × 单位（分钟/小时）。0 或空返回 null（不限时）。</summary>
    private static bool TryParseDuration(string? text, OptionItem<int>? unit, out TimeSpan? duration)
    {
        duration = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!double.TryParse(text.Trim(), out var value) || value < 0) return false;
        if (value == 0) return true;

        var minutes = value * (unit?.Value ?? 1);
        if (minutes > 60 * 24 * 30) return false; // 上限约一个月，防误输入
        duration = TimeSpan.FromMinutes(minutes);
        return true;
    }

    private static string FormatMinutes(int minutes)
        => minutes >= 60 && minutes % 60 == 0 ? $"{minutes / 60} 小时" : $"{minutes} 分钟";

    private void SetAllChecked(bool value)
    {
        foreach (var item in Scripts)
            item.IsChecked = value;
    }

    /// <summary>删除：优先删除所有勾选的脚本；一个都没勾时删除当前选中的脚本。</summary>
    private void DeleteSelectedOrChecked()
    {
        var checkedItems = Scripts.Where(s => s.IsChecked).ToList();
        if (checkedItems.Count > 0)
        {
            var names = string.Join("、", checkedItems.Select(s => s.Script.Name));
            var answer = System.Windows.MessageBox.Show(
                $"确定删除勾选的 {checkedItems.Count} 个脚本吗？\n{names}\n此操作不可恢复。",
                "批量删除脚本", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (answer != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                foreach (var item in checkedItems)
                    _store.DeleteScript(new ScriptFile(item.Script, item.FilePath));
                Load();
            }
            catch (Exception ex)
            {
                Log.Error("批量删除脚本", ex);
                _runner.ReportStatus("批量删除失败：" + ex.Message);
            }
            return;
        }

        DeleteSelected();
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        var result = System.Windows.MessageBox.Show(
            $"确定删除脚本「{_selected.Script.Name}」吗？此操作不可恢复。",
            "删除脚本", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            _store.DeleteScript(new ScriptFile(_selected.Script, _selected.FilePath));
            Load();
        }
        catch (Exception ex)
        {
            Log.Error("删除脚本", ex);
            _runner.ReportStatus("删除脚本失败：" + ex.Message);
        }
    }

    private void AddAction(string kind)
    {
        if (_selected == null) return;
        var action = ActionFactory.CreateDefault(kind);
        if (action == null) return;

        if (!ActionEditWindow.Show(action, out var edited)) return;

        int insertAt = SelectedActionIndex >= 0 ? SelectedActionIndex + 1 : _selected.Script.Actions.Count;
        _selected.Script.Actions.Insert(insertAt, edited);
        RebuildActions();
        SelectedActionIndex = insertAt;
        SaveSelected();
    }

    private void EditAction()
    {
        if (!HasActionSelection) return;
        var original = _selected!.Script.Actions[SelectedActionIndex];
        // 编辑在副本上进行，取消时保持原动作不变
        var copy = JsonSerializer.Deserialize<IAction>(JsonSerializer.Serialize((object)original, AppDataStore.JsonOpts), AppDataStore.JsonOpts);
        if (copy == null) return;

        if (!ActionEditWindow.Show(copy, out var edited)) return;

        _selected.Script.Actions[SelectedActionIndex] = edited;
        RebuildActions();
        SaveSelected();
    }

    private void DeleteAction()
    {
        if (!HasActionSelection) return;
        _selected!.Script.Actions.RemoveAt(SelectedActionIndex);
        RebuildActions();
        SaveSelected();
    }

    private bool CanMove(int delta)
    {
        if (!HasActionSelection) return false;
        var target = SelectedActionIndex + delta;
        return target >= 0 && target < _selected!.Script.Actions.Count;
    }

    private void MoveAction(int delta)
    {
        if (!CanMove(delta)) return;
        var list = _selected!.Script.Actions;
        (list[SelectedActionIndex], list[SelectedActionIndex + delta]) = (list[SelectedActionIndex + delta], list[SelectedActionIndex]);
        RebuildActions();
        SelectedActionIndex += delta;
        SaveSelected();
    }

    private string UniqueName(string baseName)
    {
        var names = Scripts.Select(s => s.Script.Name).ToHashSet(StringComparer.CurrentCulture);
        if (!names.Contains(baseName)) return baseName;
        for (int i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (!names.Contains(candidate)) return candidate;
        }
    }
}

/// <summary>按类型创建默认动作，坐标类动作默认取当前鼠标位置。</summary>
public static class ActionFactory
{
    public static IAction? CreateDefault(string kind)
    {
        var (x, y) = ScreenInfo.CursorPosition;
        return kind switch
        {
            "click" => new MouseClickAction { X = x, Y = y },
            "move" => new MouseMoveAction { X = x, Y = y },
            "drag" => new MouseDragAction { FromX = x, FromY = y, ToX = x + 200, ToY = y },
            "scroll" => new MouseScrollAction(),
            "keypress" => new KeyPressAction { VirtualKey = 0x53 }, // S
            "keyhold" => new KeyHoldAction { VirtualKey = 0x10 },   // Shift
            "text" => new TextInputAction { Text = "Hello" },
            "delay" => new DelayAction(),
            _ => null,
        };
    }
}
