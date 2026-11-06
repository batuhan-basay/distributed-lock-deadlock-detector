using System.Collections.Concurrent;
using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Events;
using LockGuard.Domain.Sessions;

namespace LockGuard.Infrastructure.Persistence.InMemory;

/// <summary>Single-process <see cref="ICoordinationStore"/> for tests and Redis-less local runs.</summary>
public sealed class InMemoryCoordinationStore(TimeProvider time) : ICoordinationStore
{
    private const int MaxEvents = 200;

    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WaitRecord> _waits = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, InstanceHeartbeat> _instances = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (AbortSignal Signal, DateTimeOffset ExpiresAt)> _aborts = new(StringComparer.Ordinal);
    private readonly LinkedList<LockEventReport> _events = new();
    private readonly Lock _eventsSync = new();

    public InMemoryCoordinationStore() : this(TimeProvider.System)
    {
    }

    public Task RegisterSessionAsync(SessionInfo session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, SessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, SessionInfo>>(new Dictionary<string, SessionInfo>(_sessions, StringComparer.Ordinal));

    public Task RecordWaitAsync(WaitRecord wait, CancellationToken cancellationToken = default)
    {
        _waits[wait.WaiterSessionId] = wait;
        return Task.CompletedTask;
    }

    public Task ClearWaitAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _waits.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WaitRecord>> GetWaitsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WaitRecord>>([.. _waits.Values]);

    public Task HeartbeatAsync(string instanceId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        _instances[instanceId] = new InstanceHeartbeat(instanceId, now);
        return Task.CompletedTask;
    }

    public Task RemoveInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        _instances.TryRemove(instanceId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, InstanceHeartbeat>> GetInstancesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, InstanceHeartbeat>>(new Dictionary<string, InstanceHeartbeat>(_instances, StringComparer.Ordinal));

    public Task SignalAbortAsync(AbortSignal signal, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _aborts[signal.SessionId] = (signal, time.GetUtcNow() + ttl);
        return Task.CompletedTask;
    }

    public Task<AbortSignal?> GetAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_aborts.TryGetValue(sessionId, out var entry))
        {
            if (entry.ExpiresAt > time.GetUtcNow())
            {
                return Task.FromResult<AbortSignal?>(entry.Signal);
            }

            _aborts.TryRemove(sessionId, out _);
        }

        return Task.FromResult<AbortSignal?>(null);
    }

    public Task ClearAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _aborts.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task AppendEventAsync(LockEventReport report, CancellationToken cancellationToken = default)
    {
        lock (_eventsSync)
        {
            _events.AddFirst(report);
            while (_events.Count > MaxEvents)
            {
                _events.RemoveLast();
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LockEventReport>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default)
    {
        lock (_eventsSync)
        {
            return Task.FromResult<IReadOnlyList<LockEventReport>>([.. _events.Take(count)]);
        }
    }
}
