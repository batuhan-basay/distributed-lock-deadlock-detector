namespace LockGuard.Application.Locking.Watchdog;

public readonly record struct LeaseRegistration(CancellationToken LostToken);
