using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using DroidUsage.Configuration;
using DroidUsage.Models;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Auth;

public sealed class WorkOsTokenRefresher : ITokenRefresher
{
    private readonly ISettingsService _settings;
    private readonly FactoryAuthFileLocator _locator;
    private readonly IFactoryAuthCrypto _crypto;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkOsTokenRefresher> _logger;

    public WorkOsTokenRefresher(
        ISettingsService settings,
        FactoryAuthFileLocator locator,
        IFactoryAuthCrypto crypto,
        HttpClient httpClient,
        ILogger<WorkOsTokenRefresher> logger)
    {
        _settings = settings;
        _locator = locator;
        _crypto = crypto;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AuthCredentials?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ManualApiKey))
        {
            return null;
        }

        var source = _locator.EnumerateSources().FirstOrDefault();
        if (source == null)
        {
            return null;
        }

        var rawJson = _crypto.DecryptFileToJson(source.Path, source.KeyBase64);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        var creds = JsonSerializer.Deserialize<AuthCredentials>(rawJson);
        if (creds == null || string.IsNullOrWhiteSpace(creds.RefreshToken))
        {
            return null;
        }

        var formFields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = creds.RefreshToken,
            ["client_id"] = FactoryEndpoints.WorkOsProductionClientId
        };

        if (!string.IsNullOrWhiteSpace(creds.ActiveOrganizationId))
        {
            formFields["organization_id"] = creds.ActiveOrganizationId;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, FactoryEndpoints.WorkOsTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formFields)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WorkOS token refresh failed with HTTP {Status}.", (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var refreshed = JsonSerializer.Deserialize<WorkOsRefreshResponse>(body);
        if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(rawJson)?.AsObject();
            if (node != null)
            {
                node["access_token"] = refreshed.AccessToken;
                node["refresh_token"] = refreshed.RefreshToken;
                _crypto.EncryptJsonToFile(source.Path, source.KeyBase64, node.ToJsonString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refreshed token could not be written back to the Factory auth file.");
        }

        creds.AccessToken = refreshed.AccessToken;
        creds.RefreshToken = refreshed.RefreshToken;
        return creds;
    }
}
