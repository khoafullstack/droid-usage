namespace DroidUsage.Services.Abstractions;

public interface IUpdateService
{
    /// <summary>False when running outside a Velopack install (e.g. from Visual Studio).</summary>
    bool IsInstalled { get; }

    /// <summary>Returns the available version string, or null when up to date or unavailable.</summary>
    Task<string?> CheckForUpdateAsync();

    /// <summary>Downloads the pending update and restarts into it. Returns false on failure.</summary>
    Task<bool> DownloadAndRestartAsync();
}
