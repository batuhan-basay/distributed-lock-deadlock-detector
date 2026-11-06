namespace LockGuard.Core;

/// <summary>Root configuration, bound from the <c>LockGuard</c> configuration section.</summary>
public sealed class LockGuardOptions
{
    public const string SectionName = "LockGuard";

    /// <summary>Logical service name stamped on every lock (shown in diagnostics).</summary>
    public string ServiceName { get; set; } = "lockguard";

    /// <summary>Unique id of this process. Defaults to <c>{machine}-{pid}-{random}</c>.</summary>
    public string InstanceId
    {
        get => field ??= $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..6]}";
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public LockAcquisitionOptions Acquisition { get; set; } = new();
    public WatchdogOptions Watchdog { get; set; } = new();
    public HeartbeatOptions Heartbeat { get; set; } = new();
    public DeadlockDetectionOptions Detection { get; set; } = new();
    public ReclaimerOptions Reclaimer { get; set; } = new();
}

public sealed class LockAcquisitionOptions
{
    /// <summary>Lease time written to the store. The watchdog keeps extending it while the work runs.</summary>
    public TimeSpan LeaseTime { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum time to wait for a contended lock before giving up.</summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>First retry delay; doubles on each retry (exponential backoff with full jitter).</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(25);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMilliseconds(400);
}

public sealed class WatchdogOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>A lease is renewed when <c>LeaseTime * RenewalRatio</c> has elapsed since the last renewal (Redisson default: 1/3).</summary>
    public double RenewalRatio { get; set; } = 1.0 / 3.0;

    /// <summary>How often the watchdog loop checks for leases that are due.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}

public sealed class HeartbeatOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed class DeadlockDetectionOptions
{
    /// <summary>When true, this process runs the detector (only the elected leader actually scans).</summary>
    public bool Enabled { get; set; }

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A cycle must be observed in this many consecutive scans before it is resolved. The snapshot of waits and holders
    /// is not atomic, so a single observation might be a "phantom deadlock".
    /// </summary>
    public int ConfirmationScans { get; set; } = 2;

    /// <summary>Wait records not refreshed within this window are ignored (waiter crashed or gave up).</summary>
    public TimeSpan WaitRecordStaleAfter { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>If a signalled victim is still blocking the cycle after this grace period, its locks are force-released.</summary>
    public TimeSpan VictimForceReleaseAfter { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan AbortSignalTtl { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class ReclaimerOptions
{
    /// <summary>When true, this process runs the orphaned lock reclaimer (only the elected leader actually scans).</summary>
    public bool Enabled { get; set; }

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>An instance whose heartbeat is older than this is considered crashed.</summary>
    public TimeSpan InstanceStaleAfter { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Locks younger than this are never reclaimed (protects against heartbeat/acquire races).</summary>
    public TimeSpan MinLockAge { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Upper bound for holding a lock. Protects against zombie processes whose watchdog keeps a lock alive forever.</summary>
    public TimeSpan MaxHoldTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Reclaim locks that were written without any expiry (legacy / buggy clients).</summary>
    public bool ReclaimLocksWithoutExpiry { get; set; } = true;
}
