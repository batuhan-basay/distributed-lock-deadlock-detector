using LockGuard.Core.Abstractions;
using LockGuard.Core.Models;

namespace LockGuard.Core.InMemory;

/// <summary>
/// Single-process <see cref="ILockStore"/> with the exact semantics of the Redis implementation (TTL, compare-and-set,
/// idempotent re-acquisition, fencing tokens). Used by unit tests and for running the API without Redis.
/// </summary>
public sealed class InMemoryLockStore(TimeProvider time) : ILockStore
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, Entry> _locks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _fences = new(StringComparer.Ordinal);

    public InMemoryLockStore() : this(TimeProvider.System)
    {
    }

    public Task<AcquireResult> TryAcquireAsync(string resource, LockOwner owner, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var now = time.GetUtcNow();
            var entry = Live(resource, now);
            if (entry is null)
            {
                var fence = _fences.TryGetValue(resource, out var f) ? f + 1 : 1;
                _fences[resource] = fence;
                _locks[resource] = new Entry(owner, now, now + ttl, fence);
                return Task.FromResult(new AcquireResult(AcquireStatus.Acquired, fence, owner.SessionId, ttl));
            }

            if (string.Equals(entry.Owner.SessionId, owner.SessionId, StringComparison.Ordinal))
            {
                // Same semantics as acquire.lua: keep acquisition time + fencing token, adopt the (possibly new) instance.
                entry.Owner = owner;
                entry.ExpiresAt = now + ttl;
                return Task.FromResult(new AcquireResult(AcquireStatus.AlreadyOwned, entry.Fence, owner.SessionId, ttl));
            }

            return Task.FromResult(AcquireResult.Contended(entry.Owner.SessionId, entry.ExpiresAt - now));
        }
    }

    public Task<bool> ExtendAsync(string resource, string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var now = time.GetUtcNow();
            var entry = Live(resource, now);
            if (entry is null || !string.Equals(entry.Owner.SessionId, sessionId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            entry.ExpiresAt = now + ttl;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ReleaseAsync(string resource, string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var entry = Live(resource, time.GetUtcNow());
            if (entry is null || !string.Equals(entry.Owner.SessionId, sessionId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            _locks.Remove(resource);
            return Task.FromResult(true);
        }
    }

    public Task<LockInfo?> GetAsync(string resource, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var now = time.GetUtcNow();
            var entry = Live(resource, now);
            return Task.FromResult(entry is null ? null : ToInfo(resource, entry, now));
        }
    }

    public Task<IReadOnlyList<LockInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var now = time.GetUtcNow();
            var result = new List<LockInfo>();
            foreach (var resource in _locks.Keys.ToList())
            {
                if (Live(resource, now) is { } entry)
                {
                    result.Add(ToInfo(resource, entry, now));
                }
            }

            return Task.FromResult<IReadOnlyList<LockInfo>>(result);
        }
    }

    /// <summary>Test helper: writes a lock "behind the manager's back", e.g. a lock left by a crashed process or without TTL.</summary>
    public void Seed(string resource, LockOwner owner, DateTimeOffset acquiredAt, TimeSpan? ttl)
    {
        lock (_sync)
        {
            var fence = _fences.TryGetValue(resource, out var f) ? f + 1 : 1;
            _fences[resource] = fence;
            _locks[resource] = new Entry(owner, acquiredAt, ttl is null ? null : acquiredAt + ttl.Value, fence);
        }
    }

    private Entry? Live(string resource, DateTimeOffset now)
    {
        if (!_locks.TryGetValue(resource, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt is { } expires && expires <= now)
        {
            _locks.Remove(resource);
            return null;
        }

        return entry;
    }

    private static LockInfo ToInfo(string resource, Entry e, DateTimeOffset now) =>
        new(resource, e.Owner.SessionId, e.Owner.ServiceName, e.Owner.InstanceId, e.AcquiredAt,
            e.ExpiresAt is null ? null : e.ExpiresAt.Value - now, e.Fence);

    private sealed class Entry(LockOwner owner, DateTimeOffset acquiredAt, DateTimeOffset? expiresAt, long fence)
    {
        public LockOwner Owner { get; set; } = owner;
        public DateTimeOffset AcquiredAt { get; } = acquiredAt;
        public DateTimeOffset? ExpiresAt { get; set; } = expiresAt;
        public long Fence { get; } = fence;
    }
}
