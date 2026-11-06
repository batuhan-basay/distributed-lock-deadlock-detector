namespace LockGuard.Application.Common.Options;

public sealed class WatchdogOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>A lease is renewed when <c>LeaseTime * RenewalRatio</c> has elapsed since the last renewal (Redisson default: 1/3).</summary>
    public double RenewalRatio { get; set; } = 1.0 / 3.0;

    /// <summary>How often the watchdog loop checks for leases that are due.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
