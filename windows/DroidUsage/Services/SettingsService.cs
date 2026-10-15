using System.Text.Json;
using DroidUsage.Models;

namespace DroidUsage.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DroidUsage");
        Directory.CreateDirectory(appDataDir);
        _settingsFilePath = Path.Combine(appDataDir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Current = loaded;
                }
            }
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
        }
    }
}
