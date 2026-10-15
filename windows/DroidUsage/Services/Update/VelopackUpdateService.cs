using System.Runtime.InteropServices;
using DroidUsage.Configuration;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace DroidUsage.Services.Update;

public sealed class VelopackUpdateService : IUpdateService
{
    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;

        // Each architecture publishes to its own channel so x64 never pulls an ARM64 package.
        var channel = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "win-arm64"
            : "win-x64";

        _manager = new UpdateManager(
            new GithubSource(FactoryEndpoints.ReleasesRepoUrl, null, false),
            new UpdateOptions { ExplicitChannel = channel });
    }

    public bool IsInstalled => _manager.IsInstalled;

    public async Task<string?> CheckForUpdateAsync()
    {
        if (!IsInstalled)
        {
            return null;
        }

        try
        {
            _pending = await _manager.CheckForUpdatesAsync();
            return _pending?.TargetFullRelease.Version.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return null;
        }
    }

    public async Task<bool> DownloadAndRestartAsync()
    {
        if (!IsInstalled || _pending is null)
        {
            return false;
        }

        try
        {
            await _manager.DownloadUpdatesAsync(_pending);
            _manager.ApplyUpdatesAndRestart(_pending);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Applying update failed.");
            return false;
        }
    }
}
