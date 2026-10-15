using DroidUsage.Models;
using DroidUsage.Services.Abstractions;

namespace DroidUsage.Presentation;

public sealed class UsagePresenter
{
    public const int MinRefreshSeconds = 15;

    private readonly IClock _clock;

    public UsagePresenter(IClock clock)
    {
        _clock = clock;
    }

    public static int NormalizeRefreshSeconds(int seconds) => Math.Max(MinRefreshSeconds, seconds);

    public bool IsInactive(UsageBucket? bucket) =>
        bucket?.WindowEnd == null || bucket.WindowEnd.Value <= _clock.UtcNow;

    public int DisplayPercent(UsageBucket? bucket) =>
        bucket == null || IsInactive(bucket)
            ? 0
            : Math.Clamp((int)Math.Ceiling(bucket.UsedPercent), 0, 100);

    public string FormatResetTime(UsageBucket? bucket, bool compact)
    {
        if (bucket?.WindowEnd == null)
        {
            return compact ? "Idle" : "Use Droid to start";
        }

        var remaining = bucket.WindowEnd.Value - _clock.UtcNow;
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
        return compact ? $"{mins}m" : $"{mins}min";
    }

    public (string Label, UsageBucket? Bucket) SelectPillBucket(UsagePoolLimits? pool, string? mode)
    {
        if (pool == null)
        {
            return ("Monthly", null);
        }

        return mode switch
        {
            "fiveHour" => ("5-hour", pool.FiveHour),
            "weekly" => ("Weekly", pool.Weekly),
            "highest" => SelectHighestBucket(pool),
            _ => ("Monthly", pool.Monthly)
        };
    }

    public string BuildTooltip(UsagePoolLimits? standard)
    {
        int monthly = DisplayPercent(standard?.Monthly);
        int weekly = DisplayPercent(standard?.Weekly);
        int fiveHour = DisplayPercent(standard?.FiveHour);
        return $"Factory Droid — Monthly: {monthly}% | Weekly: {weekly}% | 5h: {fiveHour}%";
    }

    public static double ComputeBarFillWidth(double trackWidth, int percent, double minNonZeroPx)
    {
        if (percent <= 0 || trackWidth <= 0)
        {
            return 0;
        }

        double clamped = Math.Clamp(percent, 0, 100);
        double proportional = trackWidth * (clamped / 100.0);
        return Math.Min(trackWidth, Math.Max(minNonZeroPx, proportional));
    }

    private (string Label, UsageBucket? Bucket) SelectHighestBucket(UsagePoolLimits pool)
    {
        var candidates = new (string Label, UsageBucket? Bucket)[]
        {
            ("5-hour", pool.FiveHour),
            ("Weekly", pool.Weekly),
            ("Monthly", pool.Monthly)
        };

        return candidates
            .OrderByDescending(c => DisplayPercent(c.Bucket))
            .First();
    }
}
