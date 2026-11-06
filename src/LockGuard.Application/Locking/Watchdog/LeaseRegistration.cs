namespace LockGuard.Application.Locking.Watchdog;

/// <summary>Handle returned by <see cref="LockWatchdog.Track"/>; <see cref="LostToken"/> is cancelled when the lease is lost.</summary>
public readonly record struct LeaseRegistration(CancellationToken LostToken);
