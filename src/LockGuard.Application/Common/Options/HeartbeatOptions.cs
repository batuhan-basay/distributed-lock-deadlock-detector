namespace LockGuard.Application.Common.Options;

public sealed class HeartbeatOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);
}
