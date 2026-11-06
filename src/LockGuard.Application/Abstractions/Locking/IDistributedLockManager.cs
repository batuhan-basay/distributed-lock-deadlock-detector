using LockGuard.Application.Locking;

namespace LockGuard.Application.Abstractions.Locking;

public interface IDistributedLockManager
{
    
    Task<LockSession> BeginSessionAsync(LockSessionOptions? options = null, CancellationToken cancellationToken = default);
}
