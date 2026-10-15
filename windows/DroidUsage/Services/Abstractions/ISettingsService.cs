using DroidUsage.Models;

namespace DroidUsage.Services.Abstractions;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Decrypted manual API key. Persisted protected with DPAPI.</summary>
    string ManualApiKey { get; set; }

    void Save();
}
