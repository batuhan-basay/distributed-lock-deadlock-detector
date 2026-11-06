using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;

namespace LockGuard.Application.Coordination;

/// <summary>
/// Lease-based leader election built on the lock store itself (dogfooding): only one detector / reclaimer instance in
/// the cluster acts at a time, all others stay hot standby and take over when the leader's lease expires.
/// </summary>
public sealed class LeaderElection(ILockStore store, string role, string serviceName, string instanceId, TimeSpan leaseTime)
{
    private readonly string _resource = InternalResources.Leader(role);
    private readonly LockOwner _owner = new($"leader-{role}-{instanceId}", serviceName, instanceId);

    public bool IsLeader { get; private set; }

    /// <summary>Acquires or renews leadership. Must be called more often than <paramref name="leaseTime"/>.</summary>
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
            // lease expires on its own
        }
    }
}
