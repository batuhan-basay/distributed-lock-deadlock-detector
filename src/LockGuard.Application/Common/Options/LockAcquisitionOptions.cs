namespace LockGuard.Application.Common.Options;

public sealed class LockAcquisitionOptions
{
    /// <summary>Lease time written to the store. The watchdog keeps extending it while the work runs.</summary>
    public TimeSpan LeaseTime { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum time to wait for a contended lock before giving up.</summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>First retry delay; doubles on each retry (exponential backoff with jitter).</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(25);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMilliseconds(400);
}
