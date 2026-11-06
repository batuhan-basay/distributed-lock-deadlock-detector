namespace LockGuard.Application.Common.Options;

public sealed class DeadlockDetectionOptions
{
    
    public bool Enabled { get; set; }

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int ConfirmationScans { get; set; } = 2;

    public TimeSpan WaitRecordStaleAfter { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan VictimForceReleaseAfter { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan AbortSignalTtl { get; set; } = TimeSpan.FromMinutes(1);
}
