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

    [JsonPropertyName("manualApiKeyProtected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManualApiKeyProtected { get; set; }

    // Plaintext key written by earlier versions; read once for migration, never written back.
    [JsonPropertyName("manualApiKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyManualApiKey { get; set; }
}
