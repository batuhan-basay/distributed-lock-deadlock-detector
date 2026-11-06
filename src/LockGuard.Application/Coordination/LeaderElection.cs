using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;

namespace LockGuard.Application.Coordination;

public sealed class LeaderElection(ILockStore store, string role, string serviceName, string instanceId, TimeSpan leaseTime)
{
    private readonly string _resource = InternalResources.Leader(role);
    private readonly LockOwner _owner = new($"leader-{role}-{instanceId}", serviceName, instanceId);

    public bool IsLeader { get; private set; }

    public async Task<bool> TryHoldAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await store.TryAcquireAsync(_resource, _owner, leaseTime, cancellationToken);
            IsLeader = result.IsSuccess;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            IsLeader = false;
        }

        return IsLeader;
    }

    public async Task ResignAsync()
    {
        if (!IsLeader)
        {
            return;
        }

        IsLeader = false;
        try
        {
            await store.ReleaseAsync(_resource, _owner.SessionId, CancellationToken.None);
        }
        catch
        {
            
        }
    }
}
