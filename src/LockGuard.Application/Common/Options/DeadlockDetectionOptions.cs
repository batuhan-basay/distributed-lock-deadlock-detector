namespace LockGuard.Application.Common.Options;

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
