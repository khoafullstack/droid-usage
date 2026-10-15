using System.Globalization;
using System.Text;
using System.Text.Json;
using DroidUsage.Configuration;
using DroidUsage.Models;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Auth;

public sealed class PlanTierProvider : IPlanTierProvider
{
    private readonly ILogger<PlanTierProvider> _logger;

    public PlanTierProvider(ILogger<PlanTierProvider> logger)
    {
        _logger = logger;
    }

    public string? GetCachedTier()
    {
        try
        {
            var cachePath = Path.Combine(AppPaths.FactoryDir, "org-managed-settings.cache.json");
            if (!File.Exists(cachePath))
            {
                return null;
            }

            var json = File.ReadAllText(cachePath, Encoding.UTF8);
            var cache = JsonSerializer.Deserialize<OrgManagedSettingsCache>(json);
            return FormatTierDisplayName(cache?.FactoryTier);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read cached Factory plan tier.");
            return null;
        }
    }

    public static string? FormatTierDisplayName(string? rawTier)
    {
        if (string.IsNullOrWhiteSpace(rawTier))
        {
            return null;
        }

        return rawTier.Trim().ToLowerInvariant() switch
        {
            "pro_plus" or "pro+" or "plus" => "Plus",
            "pro" => "Pro",
            "max" => "Max",
            "enterprise" => "Enterprise",
            "free" or "trial" => "Trial",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                rawTier.Replace('_', ' ').ToLowerInvariant())
        };
    }
}
