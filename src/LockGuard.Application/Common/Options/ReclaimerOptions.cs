using LockGuard.Domain.Reclamation;

namespace LockGuard.Application.Common.Options;

public sealed class ReclaimerOptions
{
    
    public bool Enabled { get; set; }

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan InstanceStaleAfter { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan MinLockAge { get; set; } = TimeSpan.FromSeconds(3);

    public TimeSpan MaxHoldTime { get; set; } = TimeSpan.FromMinutes(5);

    public bool ReclaimLocksWithoutExpiry { get; set; } = true;

    public StaleLockPolicy ToPolicy() => new(InstanceStaleAfter, MinLockAge, MaxHoldTime, ReclaimLocksWithoutExpiry);
}
