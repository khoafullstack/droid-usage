using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DroidUsage.Models;

namespace DroidUsage.Services;

public sealed record UsageFetchResult(
    bool IsSuccess,
    BillingLimitsResponse? Data,
    string? PlanTier,
    string? ErrorMessage);

public sealed class FactoryApiClient
{
    private readonly FactoryAuthService _authService;
    private readonly HttpClient _httpClient;

    public FactoryApiClient(FactoryAuthService authService, HttpClient httpClient)
    {
        _authService = authService;
        _httpClient = httpClient;
    }

    public async Task<UsageFetchResult> FetchBillingLimitsAsync(CancellationToken cancellationToken = default)
    {
        var creds = _authService.LoadCredentials();
        if (creds == null || string.IsNullOrWhiteSpace(creds.AccessToken))
        {
            return new UsageFetchResult(
                false,
                null,
                null,
                "No Factory credentials found. Log in with 'droid' CLI or set a token in Settings.");
        }

        var tier = _authService.GetCachedFactoryTier();

        try
        {
            var response = await SendLimitsRequestAsync(creds, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                var refreshed = await _authService.RefreshWorkOsTokenAsync(cancellationToken);
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
        var baseUrl = string.Equals(creds.EffectiveRegion, "eu", StringComparison.OrdinalIgnoreCase)
            ? "https://api.eu.factory.ai"
            : "https://api.factory.ai";

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/billing/limits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Factory-Client", "cli");

        if (!string.IsNullOrWhiteSpace(creds.ActiveOrganizationId))
        {
            request.Headers.TryAddWithoutValidation("X-Factory-Org-Id", creds.ActiveOrganizationId);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }
}
