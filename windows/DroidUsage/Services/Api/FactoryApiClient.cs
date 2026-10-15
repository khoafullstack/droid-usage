using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DroidUsage.Configuration;
using DroidUsage.Models;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Api;

public sealed class FactoryApiClient : IUsageApiClient
{
    private readonly ICredentialProvider _credentials;
    private readonly ITokenRefresher _tokenRefresher;
    private readonly IPlanTierProvider _planTier;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FactoryApiClient> _logger;

    public FactoryApiClient(
        ICredentialProvider credentials,
        ITokenRefresher tokenRefresher,
        IPlanTierProvider planTier,
        HttpClient httpClient,
        ILogger<FactoryApiClient> logger)
    {
        _credentials = credentials;
        _tokenRefresher = tokenRefresher;
        _planTier = planTier;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UsageFetchResult> FetchBillingLimitsAsync(CancellationToken cancellationToken = default)
    {
        var creds = _credentials.LoadCredentials();
        if (creds == null || string.IsNullOrWhiteSpace(creds.AccessToken))
        {
            return new UsageFetchResult(
                false,
                null,
                null,
                "No Factory credentials found. Log in with 'droid' CLI or set a token in Settings.");
        }

        var tier = _planTier.GetCachedTier();

        try
        {
            var response = await SendLimitsRequestAsync(creds, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                var refreshed = await _tokenRefresher.RefreshAsync(cancellationToken);
                if (refreshed != null && !string.IsNullOrWhiteSpace(refreshed.AccessToken))
                {
                    creds = refreshed;
                    response = await SendLimitsRequestAsync(creds, cancellationToken);
                }
                else
                {
                    return new UsageFetchResult(
                        false,
                        null,
                        tier,
                        "Session expired (401). Run 'droid' CLI to re-authenticate or update token.");
                }
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Factory API returned HTTP {Status}.", (int)response.StatusCode);
                    return new UsageFetchResult(
                        false,
                        null,
                        tier,
                        $"Factory API returned HTTP {(int)response.StatusCode}.");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<BillingLimitsResponse>(json);
                if (data?.Limits?.Standard == null)
                {
                    return new UsageFetchResult(
                        false,
                        null,
                        tier,
                        "Unexpected response format from Factory Billing API.");
                }

                return new UsageFetchResult(true, data, tier, null);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch billing limits.");
            return new UsageFetchResult(
                false,
                null,
                tier,
                $"Network error: {ex.Message}");
        }
    }

    private async Task<HttpResponseMessage> SendLimitsRequestAsync(
        AuthCredentials creds,
        CancellationToken cancellationToken)
    {
        var baseUrl = FactoryEndpoints.GetApiBaseUrl(creds.EffectiveRegion);

        var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + FactoryEndpoints.BillingLimitsPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Factory-Client", "cli");

        if (!string.IsNullOrWhiteSpace(creds.ActiveOrganizationId))
        {
            request.Headers.TryAddWithoutValidation("X-Factory-Org-Id", creds.ActiveOrganizationId);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }
}
