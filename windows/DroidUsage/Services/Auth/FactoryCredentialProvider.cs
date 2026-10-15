using System.Text.Json;
using DroidUsage.Models;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Auth;

public sealed class FactoryCredentialProvider : ICredentialProvider
{
    private readonly ISettingsService _settings;
    private readonly FactoryAuthFileLocator _locator;
    private readonly IFactoryAuthCrypto _crypto;
    private readonly ILogger<FactoryCredentialProvider> _logger;

    public FactoryCredentialProvider(
        ISettingsService settings,
        FactoryAuthFileLocator locator,
        IFactoryAuthCrypto crypto,
        ILogger<FactoryCredentialProvider> logger)
    {
        _settings = settings;
        _locator = locator;
        _crypto = crypto;
        _logger = logger;
    }

    public AuthCredentials? LoadCredentials()
    {
        var manualKey = _settings.ManualApiKey?.Trim();
        if (string.IsNullOrEmpty(manualKey))
        {
            manualKey = Environment.GetEnvironmentVariable("FACTORY_API_KEY")?.Trim();
        }

        if (!string.IsNullOrEmpty(manualKey))
        {
            return new AuthCredentials { AccessToken = manualKey };
        }

        foreach (var source in _locator.EnumerateSources())
        {
            var json = _crypto.DecryptFileToJson(source.Path, source.KeyBase64);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                var creds = JsonSerializer.Deserialize<AuthCredentials>(json);
                if (creds != null)
                {
                    return creds;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Factory auth file {File} contained invalid JSON.", Path.GetFileName(source.Path));
            }
        }

        return null;
    }
}
