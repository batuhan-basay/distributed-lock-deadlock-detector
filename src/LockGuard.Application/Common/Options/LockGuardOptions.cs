namespace LockGuard.Application.Common.Options;

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
