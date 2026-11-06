namespace LockGuard.Application.Common.Options;

public sealed class LockGuardOptions
{
    public const string SectionName = "LockGuard";

    public string ServiceName { get; set; } = "lockguard";

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
