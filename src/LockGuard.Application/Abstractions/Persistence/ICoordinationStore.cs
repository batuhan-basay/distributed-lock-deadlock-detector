using LockGuard.Domain.Coordination;
using LockGuard.Domain.Events;
using LockGuard.Domain.Sessions;

namespace LockGuard.Application.Abstractions.Persistence;

public interface ICoordinationStore
{
    
    Task RegisterSessionAsync(SessionInfo session, CancellationToken cancellationToken = default);
    Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, SessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default);

    Task RecordWaitAsync(WaitRecord wait, CancellationToken cancellationToken = default);
    Task ClearWaitAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitRecord>> GetWaitsAsync(CancellationToken cancellationToken = default);

    Task HeartbeatAsync(string instanceId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task RemoveInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, InstanceHeartbeat>> GetInstancesAsync(CancellationToken cancellationToken = default);

    Task SignalAbortAsync(AbortSignal signal, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<AbortSignal?> GetAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default);
    Task ClearAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default);

    Task AppendEventAsync(LockEventReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LockEventReport>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default);
}
