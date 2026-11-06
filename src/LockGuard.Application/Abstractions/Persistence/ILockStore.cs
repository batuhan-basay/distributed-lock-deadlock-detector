using LockGuard.Domain.Locks;

namespace LockGuard.Application.Abstractions.Persistence;

public interface ILockStore
{
    
    Task<AcquireResult> TryAcquireAsync(string resource, LockOwner owner, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<bool> ExtendAsync(string resource, string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(string resource, string sessionId, CancellationToken cancellationToken = default);

    Task<LockInfo?> GetAsync(string resource, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LockInfo>> ListAsync(CancellationToken cancellationToken = default);
}
