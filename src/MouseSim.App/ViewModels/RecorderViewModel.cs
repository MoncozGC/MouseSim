using System.Collections.ObjectModel;
using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.App.Services;
using MouseSim.App.Views;
using MouseSim.Core.Actions;
using MouseSim.Core.Models;

namespace MouseSim.App.ViewModels;

/// <summary>录制页：录制真实操作 → 预览 → 保存为脚本（可立即运行）。</summary>
public class RecorderViewModel : ObservableObject
{
    private readonly RecorderService _recorder;
    private readonly ScriptViewModel _scripts;
    private readonly MainViewModel _main;
    private List<IAction> _recorded = [];

    public RecorderViewModel(RecorderService recorder, ScriptViewModel scripts, MainViewModel main)
    {
        _recorder = recorder;
        _scripts = scripts;
        _main = main;
        _recorder.RecordingChanged += () => Ui.OnUi(OnRecordingChanged);
        _main.Status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.RecordHotkeyText))
            {
                OnPropertyChanged(nameof(StartRecordButtonText));
                OnPropertyChanged(nameof(StopRecordButtonText));
            }
        };

        StartRecordCommand = new RelayCommand(() => _ = StartAsync(), () => !IsRecording);
        StopRecordCommand = new RelayCommand(() => _ = StopAsync(), () => IsRecording);
        SaveCommand = new RelayCommand(() => _ = SaveAsync(runAfterSave: false), () => HasResult);
        SaveAndRunCommand = new RelayCommand(() => _ = SaveAsync(runAfterSave: true), () => HasResult);
        DiscardCommand = new RelayCommand(Discard, () => HasResult);
    }

    public ICommand StartRecordCommand { get; }
    public ICommand StopRecordCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndRunCommand { get; }
    public ICommand DiscardCommand { get; }

    /// <summary>供全局热键（F9）使用：开始/停止切换。</summary>
    public void ToggleRecording()
    {
        if (IsRecording) _ = StopAsync();
        else _ = StartAsync();
    }

    public ObservableCollection<ActionListItem> ResultActions { get; } = [];

    public bool IsRecording => _recorder.IsRecording;

    public bool HasResult => ResultActions.Count > 0;

    public string StartRecordButtonText => $"● 开始录制（{_main.Status.RecordHotkeyText}）";
    public string StopRecordButtonText => $"■ 停止录制（{_main.Status.RecordHotkeyText}）";

    private string _liveText = "尚未录制";
    public string LiveText { get => _liveText; set => SetProperty(ref _liveText, value); }

    private async Task StartAsync()
    {
        try
        {
            ResultActions.Clear();
            _recorded = [];
            OnPropertyChanged(nameof(HasResult));
            LiveText = "正在录制，请去目标程序操作（本程序窗口内的点击不会被记录）";
            await _recorder.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error("开始录制", ex);
            LiveText = "录制失败：" + ex.Message;
        }
        finally
        {
            OnRecordingChanged();
        }
    }

    private async Task StopAsync()
    {
        try
        {
            var actions = await _recorder.StopAsync();
            _recorded = actions;
            ResultActions.Clear();
            for (int i = 0; i < actions.Count; i++)
                ResultActions.Add(new ActionListItem(i + 1, ActionTypeLabels.Of(actions[i]), actions[i].Describe()));
            OnPropertyChanged(nameof(HasResult));
            LiveText = actions.Count == 0
                ? "没有录到有效操作（点击、拖拽、滚轮或按键）"
                : $"录制完成：{actions.Count} 个动作，可保存为脚本";
        }
        catch (Exception ex)
        {
            Log.Error("停止录制", ex);
            LiveText = "停止录制失败：" + ex.Message;
        }
        finally
        {
            OnRecordingChanged();
        }
    }

    private async Task SaveAsync(bool runAfterSave)
    {
        if (_recorded.Count == 0) return;

        var defaultName = $"录制 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        var name = InputDialog.Show("保存录制", "脚本名称：", defaultName);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var script = new Script
            {
                Name = name.Trim(),
                LoopCount = 1,
                StepDelayMs = 0,
                Actions = [.. _recorded],
            };
            await _scripts.AddScriptAsync(script);

            Discard();
            _main.ActiveTab = MainViewModel.ScriptsTabIndex;

            if (runAfterSave)
                _scripts.RunSelected();
            else
                _main.Status.SetText($"已保存「{script.Name}」，按 F6 运行");
        }
        catch (Exception ex)
        {
            Log.Error("保存录制", ex);
            LiveText = "保存失败：" + ex.Message;
        }
    }

    private void Discard()
    {
        _recorded = [];
        ResultActions.Clear();
        OnPropertyChanged(nameof(HasResult));
        LiveText = "尚未录制";
    }

    private void OnRecordingChanged()
    {
        OnPropertyChanged(nameof(IsRecording));
        CommandManager.InvalidateRequerySuggested();
        if (_recorder.IsRecording)
            LiveText = "正在录制，请去目标程序操作（F9 或「停止录制」结束）";
    }
}
