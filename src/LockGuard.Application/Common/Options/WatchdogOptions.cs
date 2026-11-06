namespace LockGuard.Application.Common.Options;

public sealed class WatchdogOptions
{
    public bool Enabled { get; set; } = true;

    public double RenewalRatio { get; set; } = 1.0 / 3.0;

    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
