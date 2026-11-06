using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Exceptions;
using LockGuard.Domain.Locks;
using LockGuard.Domain.Sessions;

namespace LockGuard.Application.Locking;

public sealed class LockSession : IAsyncDisposable
{
    private readonly DistributedLockManager _manager;
    private readonly Dictionary<string, HeldLock> _held = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lockLost = new();
    private readonly bool _watchdogEnabled;
    private int _disposed;

    internal LockSession(DistributedLockManager manager, SessionInfo info, LockSessionOptions options)
    {
        _manager = manager;
        Info = info;
        LeaseTime = options.LeaseTime ?? manager.Options.Acquisition.LeaseTime;
        WaitTimeout = options.WaitTimeout ?? manager.Options.Acquisition.WaitTimeout;
        _watchdogEnabled = !options.DisableWatchdog && manager.Options.Watchdog.Enabled;
    }

    public SessionInfo Info { get; }
    public string SessionId => Info.SessionId;
    public TimeSpan LeaseTime { get; }
    public TimeSpan WaitTimeout { get; }

    public CancellationToken LockLost => _lockLost.Token;

    public string? LockLostReason { get; private set; }

    public IReadOnlyDictionary<string, int> HeldResources
    {
        get
        {
            _gate.Wait();
            try
            {
                return _held.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public async Task<LockHandle> AcquireAsync(string resource, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_held.TryGetValue(resource, out var held))
            {
                held.Count++;
                _manager.Telemetry.Acquisitions.Add(1,
                    new("resource_group", ResourceGroup.Of(resource)), new("outcome", "reentered"));
                return new LockHandle(this, resource, held.FencingToken, isReentrant: true);
            }
        }
        finally
        {
            _gate.Release();
        }

        AcquireResult result;
        try
        {
            result = await _manager.AcquireCoreAsync(this, resource, cancellationToken);
        }
        catch (DistributedLockException ex) when (ex is DeadlockVictimException or LockLostException)
        {
            
            await ReleaseAllAsync();
            throw;
        }

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var lease = _watchdogEnabled
                ? _manager.Watchdog.Track(resource, SessionId, LeaseTime, OnLeaseLost)
                : default;

            _held[resource] = new HeldLock(result.FencingToken, _manager.Time.GetTimestamp(), lease.LostToken);
            _manager.Telemetry.HeldLocks.Add(1, new KeyValuePair<string, object?>("resource_group", ResourceGroup.Of(resource)));
            return new LockHandle(this, resource, result.FencingToken, isReentrant: result.Status == AcquireStatus.AlreadyOwned);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LockHandle>> AcquireOrderedAsync(IEnumerable<string> resources, CancellationToken cancellationToken = default)
    {
        var ordered = resources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var handles = new List<LockHandle>(ordered.Count);
        try
        {
            foreach (var resource in ordered)
            {
                handles.Add(await AcquireAsync(resource, cancellationToken));
            }

            return handles;
        }
        catch
        {
            foreach (var handle in handles)
            {
                await handle.DisposeAsync();
            }

            throw;
        }
    }

    public void EnsureLocksHeld()
    {
        if (_lockLost.IsCancellationRequested)
        {
            throw new LockLostException(string.Join(',', HeldResources.Keys), SessionId, LockLostReason ?? "lock lost");
        }
    }

    public async Task EnsureLocksHeldAsync(CancellationToken cancellationToken = default)
    {
        EnsureLocksHeld();
        foreach (var resource in HeldResources.Keys)
        {
            var info = await _manager.Store.GetAsync(resource, cancellationToken);
            if (info is null || !string.Equals(info.SessionId, SessionId, StringComparison.Ordinal))
            {
                OnLeaseLost($"lock on '{resource}' is no longer owned by this session (lease expired or reclaimed)");
                throw new LockLostException(resource, SessionId, LockLostReason!);
            }
        }
    }

    internal async ValueTask ReleaseOneAsync(string resource)
    {
        HeldLock? toRelease = null;
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (!_held.TryGetValue(resource, out var held))
            {
                return; 
            }

            if (--held.Count > 0)
            {
                return; 
            }

            _held.Remove(resource);
            toRelease = held;
        }
        finally
        {
            _gate.Release();
        }

        await ReleaseInStoreAsync(resource, toRelease);
    }

    private async Task ReleaseAllAsync()
    {
        List<KeyValuePair<string, HeldLock>> all;
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            all = [.. _held];
            _held.Clear();
        }
        finally
        {
            _gate.Release();
        }

        foreach (var (resource, held) in all)
        {
            await ReleaseInStoreAsync(resource, held);
        }
    }

    private async Task ReleaseInStoreAsync(string resource, HeldLock held)
    {
        _manager.Watchdog.Untrack(resource, SessionId);
        var group = new KeyValuePair<string, object?>("resource_group", ResourceGroup.Of(resource));
        _manager.Telemetry.HeldLocks.Add(-1, group);
        _manager.Telemetry.HoldDuration.Record(_manager.Time.GetElapsedTime(held.AcquiredTimestamp).TotalSeconds, group);

        try
        {
            await _manager.Store.ReleaseAsync(resource, SessionId, CancellationToken.None);
        }
        catch
        {
            
        }
    }

    private void OnLeaseLost(string reason)
    {
        LockLostReason ??= reason;
        try
        {
            _lockLost.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await ReleaseAllAsync();

        try
        {
            await _manager.Coordination.ClearWaitAsync(SessionId, CancellationToken.None);
            await _manager.Coordination.RemoveSessionAsync(SessionId, CancellationToken.None);
        }
        catch
        {
            
        }
    }

    private sealed class HeldLock(long fencingToken, long acquiredTimestamp, CancellationToken lostToken)
    {
        public long FencingToken { get; } = fencingToken;
        public long AcquiredTimestamp { get; } = acquiredTimestamp;
        public CancellationToken LostToken { get; } = lostToken;
        public int Count { get; set; } = 1;
    }
}
