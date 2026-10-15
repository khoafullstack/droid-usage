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

    [JsonIgnore]
    public double ExtraUsageBalanceDollars => ExtraUsageBalanceCents / 100.0;

    [JsonIgnore]
    public string FormattedExtraBalance => $"${ExtraUsageBalanceDollars:F2}";
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

    [JsonIgnore]
    public bool IsExpiredOrInactive =>
        WindowEnd == null || WindowEnd.Value <= DateTimeOffset.UtcNow;

    [JsonIgnore]
    public int DisplayPercent =>
        IsExpiredOrInactive ? 0 : Math.Clamp((int)Math.Ceiling(UsedPercent), 0, 100);

    public string GetFormattedResetTime(bool compact = false)
    {
        if (WindowEnd == null)
        {
            return compact ? "Idle" : "Use Droid to start";
        }

        var remaining = WindowEnd.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return compact ? "Idle" : "Use Droid to start";
        }

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        var minutes = remaining.Minutes;

        if (totalHours >= 24)
        {
            var days = totalHours / 24;
            return compact
                ? $"{days}d"
                : $"{days} day{(days == 1 ? "" : "s")}";
        }

        if (totalHours > 0)
        {
            return compact
                ? $"{totalHours}h {minutes}m"
                : $"{totalHours}h {minutes}min";
        }

        var mins = Math.Max(1, minutes);
        return compact
            ? $"{mins}m"
            : $"{mins}min";
    }
}
