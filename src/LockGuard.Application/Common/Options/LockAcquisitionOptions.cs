namespace LockGuard.Application.Common.Options;

public sealed class LockAcquisitionOptions
{
    
    public TimeSpan LeaseTime { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(25);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMilliseconds(400);
}
