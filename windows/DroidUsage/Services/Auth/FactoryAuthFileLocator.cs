using System.Text;
using DroidUsage.Configuration;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Auth;

public sealed record AuthFileSource(string Path, string KeyBase64);

/// <summary>Finds the encrypted Factory CLI auth files and their keys, in priority order.</summary>
public sealed class FactoryAuthFileLocator
{
    private const string KeyringTargetName = "Factory CLI/auth-encryption-key";

    private readonly ISecretStore _secretStore;
    private readonly ILogger<FactoryAuthFileLocator> _logger;

    public FactoryAuthFileLocator(ISecretStore secretStore, ILogger<FactoryAuthFileLocator> logger)
    {
        _secretStore = secretStore;
        _logger = logger;
    }

    public IEnumerable<AuthFileSource> EnumerateSources()
    {
        var keyringPath = Path.Combine(AppPaths.FactoryDir, "auth.v2.keyring");
        if (File.Exists(keyringPath))
        {
            var keyB64 = _secretStore.GetGenericPassword(KeyringTargetName);
            if (!string.IsNullOrWhiteSpace(keyB64))
            {
                yield return new AuthFileSource(keyringPath, keyB64);
            }
        }

        var fileAuthPath = Path.Combine(AppPaths.FactoryDir, "auth.v2.file");
        var keyFilePath = Path.Combine(AppPaths.FactoryDir, "auth.v2.key");
        if (File.Exists(fileAuthPath) && File.Exists(keyFilePath))
        {
            string? keyB64 = null;
            try
            {
                keyB64 = File.ReadAllText(keyFilePath, Encoding.UTF8).Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read Factory auth key file.");
            }

            if (!string.IsNullOrWhiteSpace(keyB64))
            {
                yield return new AuthFileSource(fileAuthPath, keyB64);
            }
        }
    }
}
