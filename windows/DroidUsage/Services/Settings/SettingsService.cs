using System.Text.Json;
using DroidUsage.Configuration;
using DroidUsage.Models;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ISecretProtector _protector;
    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; } = new();

    public string ManualApiKey { get; set; } = string.Empty;

    public SettingsService(ISecretProtector protector, ILogger<SettingsService> logger)
    {
        _protector = protector;
        _logger = logger;
        Directory.CreateDirectory(AppPaths.AppDataDir);
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
            {
                return;
            }

            var json = File.ReadAllText(AppPaths.SettingsFile);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded == null)
            {
                return;
            }

            Current = loaded;

            if (!string.IsNullOrWhiteSpace(loaded.LegacyManualApiKey))
            {
                ManualApiKey = loaded.LegacyManualApiKey.Trim();
                _logger.LogInformation("Migrating plaintext manual API key to DPAPI-protected storage.");
                Save();
            }
            else if (!string.IsNullOrEmpty(loaded.ManualApiKeyProtected))
            {
                ManualApiKey = _protector.Unprotect(loaded.ManualApiKeyProtected) ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings; falling back to defaults.");
            Current = new AppSettings();
            ManualApiKey = string.Empty;
        }
    }

    public void Save()
    {
        try
        {
            Current.LegacyManualApiKey = null;
            Current.ManualApiKeyProtected = string.IsNullOrWhiteSpace(ManualApiKey)
                ? null
                : _protector.Protect(ManualApiKey.Trim());

            var json = JsonSerializer.Serialize(Current, JsonOptions);
            var tempPath = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, AppPaths.SettingsFile, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings.");
        }
    }
}
