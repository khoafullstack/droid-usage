using System.Text.Json.Serialization;

namespace DroidUsage.Models;

public sealed class AuthCredentials
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("active_organization_id")]
    public string? ActiveOrganizationId { get; set; }

    [JsonPropertyName("whoami")]
    public WhoamiMetadata? Whoami { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonIgnore]
    public string EffectiveRegion =>
        Whoami?.Region ?? Region ?? "global";
}

public sealed class WhoamiMetadata
{
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("inferenceRegion")]
    public string? InferenceRegion { get; set; }

    [JsonPropertyName("isEnterprise")]
    public bool? IsEnterprise { get; set; }

    [JsonPropertyName("isOnPrem")]
    public bool? IsOnPrem { get; set; }

    [JsonPropertyName("principalKind")]
    public string? PrincipalKind { get; set; }
}

public sealed class OrgManagedSettingsCache
{
    [JsonPropertyName("orgId")]
    public string? OrgId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("factoryTier")]
    public string? FactoryTier { get; set; }
}

public sealed class WorkOsRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}
