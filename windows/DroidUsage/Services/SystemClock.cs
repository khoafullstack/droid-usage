using DroidUsage.Services.Abstractions;

namespace DroidUsage.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTime Now => DateTime.Now;
}
