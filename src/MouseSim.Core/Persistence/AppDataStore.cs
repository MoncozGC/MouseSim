using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MouseSim.Core.Hotkeys;
using MouseSim.Core.Models;

namespace MouseSim.Core.Persistence;

/// <summary>脚本文件包装：脚本对象与其磁盘文件的对应关系。</summary>
public sealed record ScriptFile(Script Script, string FilePath);

/// <summary>
/// 脚本与设置的落盘存储。
/// 数据保存在 %APPDATA%\MouseSim\ 下：settings.json 与 scripts\*.json。
/// </summary>
public sealed class AppDataStore
{
    public static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseSim");

    private static readonly string ScriptsDir = Path.Combine(AppDataDir, "scripts");
    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppDataStore()
    {
        Directory.CreateDirectory(ScriptsDir);
    }

    public List<ScriptFile> LoadScripts()
    {
        var result = new List<ScriptFile>();
        foreach (var file in Directory.EnumerateFiles(ScriptsDir, "*.json"))
        {
            try
            {
                var script = JsonSerializer.Deserialize<Script>(File.ReadAllText(file), JsonOpts);
                if (script != null)
                    result.Add(new ScriptFile(script, file));
            }
            catch (JsonException)
            {
                // 单个文件损坏不影响其他脚本
            }
        }
        return result.OrderBy(s => s.Script.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>保存脚本。新建脚本返回其文件路径；重命名时负责删除旧文件。</summary>
    public async Task<ScriptFile> SaveScriptAsync(Script script, string? oldFilePath = null)
    {
        if (oldFilePath != null && File.Exists(oldFilePath))
            File.Delete(oldFilePath);

        var path = Path.Combine(ScriptsDir, SafeFileName(script.Name) + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(script, JsonOpts));
        return new ScriptFile(script, path);
    }

    public void DeleteScript(ScriptFile file)
    {
        if (File.Exists(file.FilePath))
            File.Delete(file.FilePath);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(SettingsFile), JsonOpts);
                if (settings != null)
                    return settings;
            }
        }
        catch (JsonException)
        {
            // 配置损坏则回退默认
        }
        return new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
        => await File.WriteAllTextAsync(SettingsFile, JsonSerializer.Serialize(settings, JsonOpts));

    private static string SafeFileName(string name)
    {
        var chars = name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        var safe = new string(chars).Trim();
        return safe.Length == 0 ? "未命名" : safe;
    }
}
