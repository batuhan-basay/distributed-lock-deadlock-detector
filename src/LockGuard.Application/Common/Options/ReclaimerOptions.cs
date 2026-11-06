using LockGuard.Domain.Reclamation;

namespace LockGuard.Application.Common.Options;

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

    /// <summary>The domain rule set built from these settings.</summary>
    public StaleLockPolicy ToPolicy() => new(InstanceStaleAfter, MinLockAge, MaxHoldTime, ReclaimLocksWithoutExpiry);
}
