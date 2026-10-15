using System.Text.Json.Serialization;

namespace DroidUsage.Models;

public sealed class BillingLimitsResponse
{
    [JsonPropertyName("usesTokenRateLimitsBilling")]
    public bool UsesTokenRateLimitsBilling { get; set; }

    [JsonPropertyName("limits")]
    public LimitsContainer? Limits { get; set; }

    [JsonPropertyName("overagePreference")]
    public string? OveragePreference { get; set; }

    [JsonPropertyName("canManageOverage")]
    public bool CanManageOverage { get; set; }

    [JsonPropertyName("extraUsageBalanceCents")]
    public long ExtraUsageBalanceCents { get; set; }

    [JsonPropertyName("extraUsageAllowed")]
    public bool ExtraUsageAllowed { get; set; }
}

public sealed class LimitsContainer
{
    [JsonPropertyName("standard")]
    public UsagePoolLimits? Standard { get; set; }

    [JsonPropertyName("core")]
    public UsagePoolLimits? Core { get; set; }
}

public sealed class UsagePoolLimits
{
    [JsonPropertyName("fiveHour")]
    public UsageBucket? FiveHour { get; set; }

    [JsonPropertyName("weekly")]
    public UsageBucket? Weekly { get; set; }

    [JsonPropertyName("monthly")]
    public UsageBucket? Monthly { get; set; }
}

public sealed class UsageBucket
{
    [JsonPropertyName("usedPercent")]
    public double UsedPercent { get; set; }

    [JsonPropertyName("windowEnd")]
    public DateTimeOffset? WindowEnd { get; set; }

    [JsonPropertyName("secondsRemaining")]
    public long? SecondsRemaining { get; set; }
}
