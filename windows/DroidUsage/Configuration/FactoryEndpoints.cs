namespace DroidUsage.Configuration;

public static class FactoryEndpoints
{
    public const string GlobalApiBaseUrl = "https://api.factory.ai";
    public const string EuApiBaseUrl = "https://api.eu.factory.ai";
    public const string BillingLimitsPath = "/api/billing/limits";
    public const string WorkOsTokenEndpoint = "https://api.workos.com/user_management/authenticate";
    public const string WorkOsProductionClientId = "client_01HNM792M5G5G1A2THWPXKFMXB";

    public const string ReleasesRepoUrl = "https://github.com/khoafullstack/droid-usage";

    public static string GetApiBaseUrl(string? region) =>
        string.Equals(region, "eu", StringComparison.OrdinalIgnoreCase)
            ? EuApiBaseUrl
            : GlobalApiBaseUrl;
}
