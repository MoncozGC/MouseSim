using System.Windows.Input;
using MouseSim.App.Infrastructure;
using MouseSim.Core.Hotkeys;
using MouseSim.Core.Persistence;

namespace MouseSim.App.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly AppDataStore _store;
    private AppSettings _model = new();

    /// <summary>保存设置（热键）后由 AppServices 订阅，负责重新注册全局热键。</summary>
    public event Action? HotkeysChanged;

    public SettingsViewModel(AppDataStore store)
    {
        _store = store;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        ResetCommand = new RelayCommand(() =>
        {
            _model.StartHotkey = new HotkeyDefinition { VirtualKey = 0x75 };  // F6
            _model.StopHotkey = new HotkeyDefinition { VirtualKey = 0x77 };   // F8
            _model.RecordHotkey = new HotkeyDefinition { VirtualKey = 0x78 }; // F9
            OnPropertyChanged(nameof(StartHotkey));
            OnPropertyChanged(nameof(StopHotkey));
            OnPropertyChanged(nameof(RecordHotkey));
            _ = SaveAsync();
        });
    }

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }

    public HotkeyDefinition StartHotkey
    {
        get => _model.StartHotkey;
        set
        {
            if (value == null) return;
            _model.StartHotkey = value;
            OnPropertyChanged();
        }
    }

    public HotkeyDefinition StopHotkey
    {
        get => _model.StopHotkey;
        set
        {
            if (value == null) return;
            _model.StopHotkey = value;
            OnPropertyChanged();
        }
    }

    public HotkeyDefinition RecordHotkey
    {
        get => _model.RecordHotkey;
        set
        {
            if (value == null) return;
            _model.RecordHotkey = value;
            OnPropertyChanged();
        }
    }

    public string DataFolder => AppDataStore.AppDataDir;

    private string _savedText = "";
    public string SavedText { get => _savedText; set => SetProperty(ref _savedText, value); }

    public void LoadFrom(AppSettings settings)
    {
        _model = settings;
        OnPropertyChanged(nameof(StartHotkey));
        OnPropertyChanged(nameof(StopHotkey));
        OnPropertyChanged(nameof(RecordHotkey));
    }

    private async Task SaveAsync()
    {
        try
        {
            await _store.SaveSettingsAsync(_model);
            SavedText = "已保存，热键即刻生效";
            HotkeysChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("保存设置", ex);
            SavedText = "保存失败：" + ex.Message;
        }
    }
}
