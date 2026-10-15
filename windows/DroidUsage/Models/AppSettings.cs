using System.Text.Json.Serialization;

namespace DroidUsage.Models;

public sealed class AppSettings
{
    [JsonPropertyName("windowX")]
    public int? WindowX { get; set; }

    [JsonPropertyName("windowY")]
    public int? WindowY { get; set; }

    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = false;

    [JsonPropertyName("pillDisplayBucket")]
    public string PillDisplayBucket { get; set; } = "monthly";

    [JsonPropertyName("refreshIntervalSeconds")]
    public int RefreshIntervalSeconds { get; set; } = 60;

    [JsonPropertyName("manualApiKey")]
    public string ManualApiKey { get; set; } = string.Empty;
}
