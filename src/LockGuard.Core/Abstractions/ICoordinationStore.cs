using LockGuard.Core.Models;

namespace LockGuard.Core.Abstractions;

/// <summary>
/// Shared, advisory metadata used for observability and deadlock/orphan handling:
/// wait records (edges of the Wait-For Graph), session metadata, instance heartbeats,
/// abort signals and the audit log. Losing this data never breaks mutual exclusion; it only
/// delays detection, which is why it lives outside the quorum-protected <see cref="ILockStore"/>.
/// </summary>
public interface ICoordinationStore
{
    // --- Sessions ---------------------------------------------------------------------------
    Task RegisterSessionAsync(SessionInfo session, CancellationToken cancellationToken = default);
    Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, SessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default);

    // --- Wait-For Graph edges ---------------------------------------------------------------
    Task RecordWaitAsync(WaitRecord wait, CancellationToken cancellationToken = default);
    Task ClearWaitAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitRecord>> GetWaitsAsync(CancellationToken cancellationToken = default);

    // --- Instance liveness ------------------------------------------------------------------
    Task HeartbeatAsync(string instanceId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task RemoveInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, InstanceHeartbeat>> GetInstancesAsync(CancellationToken cancellationToken = default);

    // --- Abort signals ----------------------------------------------------------------------
    Task SignalAbortAsync(AbortSignal signal, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<AbortSignal?> GetAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default);
    Task ClearAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default);

    // --- Audit log ----------------------------------------------------------------------------
    Task AppendEventAsync(LockEventReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LockEventReport>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default);
}
