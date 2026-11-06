using System.Diagnostics;
using LockGuard.Core.Abstractions;
using LockGuard.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LockGuard.Redis;

/// <summary>
/// <see cref="ILockStore"/> implementing the Redlock algorithm over N independent Redis masters:
/// <list type="number">
///   <item>record the start time;</item>
///   <item>run the atomic acquire script on all N nodes in parallel, each with a small per-node timeout;</item>
///   <item>the lock is acquired iff at least N/2+1 nodes granted it AND the remaining validity
///         <c>ttl - elapsed - drift</c> is positive (drift = <c>ttl * ClockDriftFactor + 2ms</c>);</item>
///   <item>otherwise release it on every node (compare-and-delete) so a failed attempt never blocks others.</item>
/// </list>
/// Extensions (watchdog) follow the same quorum rule. Ownership is per <em>session id</em>, which makes acquisition
/// idempotent across retries and processes.
/// </summary>
public sealed partial class RedlockLockStore : ILockStore
{
    private readonly RedisNodePool _pool;
    private readonly TimeProvider _time;
    private readonly ILogger<RedlockLockStore> _logger;
    private readonly string _lockPrefix;
    private readonly RedisKey _fenceKey;

    public RedlockLockStore(RedisNodePool pool, TimeProvider time, ILogger<RedlockLockStore> logger)
    {
        _pool = pool;
        _time = time;
        _logger = logger;
        _lockPrefix = pool.Options.KeyPrefix + "lock:";
        _fenceKey = pool.Options.KeyPrefix + "fence";
    }

    // Hash tag around the resource keeps the key layout cluster-friendly should a node itself be a Redis Cluster.
    private RedisKey LockKey(string resource) => $"{_lockPrefix}{{{resource}}}";

    private string? ResourceFromKey(string key)
    {
        if (!key.StartsWith(_lockPrefix, StringComparison.Ordinal) || key.Length < _lockPrefix.Length + 2)
        {
            return null;
        }

        var body = key[_lockPrefix.Length..];
        return body.StartsWith('{') && body.EndsWith('}') ? body[1..^1] : body;
    }

    public async Task<AcquireResult> TryAcquireAsync(string resource, LockOwner owner, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var start = _time.GetTimestamp();
        var key = LockKey(resource);
        var ttlMs = (long)ttl.TotalMilliseconds;
        RedisValue[] args =
        [
            owner.SessionId, ttlMs, owner.ServiceName, owner.InstanceId, _time.GetUtcNow().ToUnixTimeMilliseconds(),
        ];

        var votes = await OnAllNodesAsync(db => db.ScriptEvaluateAsync(LuaScripts.Acquire, [key, _fenceKey], args), cancellationToken);
        var parsed = votes.Select(v => v is null ? (NodeVote?)null : NodeVote.Parse(v)).ToList();

        var granted = parsed.Where(v => v is { Status: >= 1 }).Select(v => v!.Value).ToList();
        var elapsed = _time.GetElapsedTime(start);
        var drift = TimeSpan.FromMilliseconds((ttl.TotalMilliseconds * _pool.Options.ClockDriftFactor) + 2);
        var validity = ttl - elapsed - drift;

        if (granted.Count >= _pool.Quorum && validity > TimeSpan.Zero)
        {
            var alreadyOwned = granted.Count(v => v.Status == 2) >= _pool.Quorum;
            return new AcquireResult(
                alreadyOwned ? AcquireStatus.AlreadyOwned : AcquireStatus.Acquired,
                granted.Max(v => v.Value),
                owner.SessionId,
                validity);
        }

        // Failed: undo the partial acquisition on every node (only deletes keys we own).
        if (granted.Count > 0)
        {
            LogQuorumFailed(resource, granted.Count, _pool.Nodes.Count, elapsed.TotalMilliseconds);
            await OnAllNodesAsync(db => db.ScriptEvaluateAsync(LuaScripts.Release, [key], [owner.SessionId]), CancellationToken.None);
        }

        var contended = parsed.Where(v => v is { Status: 0 }).Select(v => v!.Value).ToList();
        var holder = contended
            .GroupBy(v => v.Owner, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        TimeSpan? holderTtl = contended.Count > 0 && contended.Min(v => v.Value) > 0
            ? TimeSpan.FromMilliseconds(contended.Min(v => v.Value))
            : null;

        return AcquireResult.Contended(holder, holderTtl);
    }

    public async Task<bool> ExtendAsync(string resource, string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var start = _time.GetTimestamp();
        var key = LockKey(resource);
        var results = await OnAllNodesAsync(
            db => db.ScriptEvaluateAsync(LuaScripts.Extend, [key], [sessionId, (long)ttl.TotalMilliseconds]),
            cancellationToken);

        var extended = results.Count(r => r is not null && (long)r == 1);
        var validity = ttl - _time.GetElapsedTime(start);
        return extended >= _pool.Quorum && validity > TimeSpan.Zero;
    }

    public async Task<bool> ReleaseAsync(string resource, string sessionId, CancellationToken cancellationToken = default)
    {
        var key = LockKey(resource);
        var results = await OnAllNodesAsync(db => db.ScriptEvaluateAsync(LuaScripts.Release, [key], [sessionId]), cancellationToken);
        return results.Any(r => r is not null && (long)r == 1);
    }

    public async Task<LockInfo?> GetAsync(string resource, CancellationToken cancellationToken = default)
    {
        var key = LockKey(resource);
        var snapshots = await OnAllNodesAsync(db => db.ScriptEvaluateAsync(LuaScripts.Inspect, [key]), cancellationToken);
        var now = _time.GetUtcNow();
        return Merge(resource, snapshots.Select(s => ParseSnapshot(s, now)).ToList());
    }

    public async Task<IReadOnlyList<LockInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        // SCAN every node (a lock exists only where a quorum agrees, but any node can tell us which keys to inspect).
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in _pool.Nodes)
        {
            if (!node.IsConnected)
            {
                continue;
            }

            try
            {
                foreach (var server in node.GetServers().Where(s => s.IsConnected && !s.IsReplica))
                {
                    await foreach (var key in server.KeysAsync(pattern: $"{_lockPrefix}*", pageSize: 500).WithCancellation(cancellationToken))
                    {
                        if (ResourceFromKey(key.ToString()) is { } resource)
                        {
                            resources.Add(resource);
                        }
                    }
                }
            }
            catch (RedisException ex)
            {
                LogNodeError(ex, "SCAN");
            }
        }

        var infos = await Task.WhenAll(resources.Select(r => GetAsync(r, cancellationToken)));
        return [.. infos.Where(i => i is not null).Select(i => i!)];
    }

    /// <summary>Quorum view of a lock: the owner seen on at least N/2+1 nodes, or <c>null</c>.</summary>
    private LockInfo? Merge(string resource, IReadOnlyList<NodeSnapshot?> snapshots)
    {
        var winner = snapshots
            .Where(s => s is not null)
            .Select(s => s!.Value)
            .GroupBy(s => s.Owner, StringComparer.Ordinal)
            .Where(g => g.Count() >= _pool.Quorum)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (winner is null)
        {
            return null;
        }

        var agreeing = winner.ToList();
        var first = agreeing[0];
        TimeSpan? ttl = agreeing.Any(s => s.PttlMs < 0) ? null : TimeSpan.FromMilliseconds(agreeing.Min(s => s.PttlMs));
        return new LockInfo(
            resource,
            first.Owner,
            first.Service,
            first.Instance,
            DateTimeOffset.FromUnixTimeMilliseconds(agreeing.Min(s => s.AcquiredMs)),
            ttl,
            agreeing.Max(s => s.Fence));
    }

    private static NodeSnapshot? ParseSnapshot(RedisResult? result, DateTimeOffset now)
    {
        if (result is null || result.IsNull)
        {
            return null;
        }

        var parts = (RedisResult[])result!;
        if (parts.Length < 6)
        {
            return null;
        }

        return new NodeSnapshot(
            PttlMs: (long)parts[0],
            Owner: (string?)parts[1] ?? string.Empty,
            Service: (string?)parts[2] ?? string.Empty,
            Instance: (string?)parts[3] ?? string.Empty,
            AcquiredMs: long.TryParse((string?)parts[4], out var acquired) ? acquired : now.ToUnixTimeMilliseconds(),
            Fence: long.TryParse((string?)parts[5], out var fence) ? fence : 0);
    }

    /// <summary>Runs <paramref name="operation"/> on every node in parallel; a failed / slow node yields <c>null</c>.</summary>
    private async Task<RedisResult?[]> OnAllNodesAsync(Func<IDatabase, Task<RedisResult>> operation, CancellationToken cancellationToken)
    {
        var timeout = _pool.Options.NodeTimeout;
        var tasks = _pool.Nodes.Select(async node =>
        {
            try
            {
                return (RedisResult?)await operation(node.GetDatabase()).WaitAsync(timeout, cancellationToken);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                LogNodeError(ex, node.Configuration);
                return null;
            }
        });

        return await Task.WhenAll(tasks);
    }

    private readonly record struct NodeVote(int Status, long Value, string Owner)
    {
        public static NodeVote Parse(RedisResult result)
        {
            var parts = (RedisResult[])result!;
            return new NodeVote((int)parts[0], (long)parts[1], (string?)parts[2] ?? string.Empty);
        }
    }

    private readonly record struct NodeSnapshot(long PttlMs, string Owner, string Service, string Instance, long AcquiredMs, long Fence);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Redlock quorum not reached for {Resource}: {Granted}/{Nodes} votes in {ElapsedMs:F1} ms")]
    private partial void LogQuorumFailed(string resource, int granted, int nodes, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis node error ({Node})")]
    private partial void LogNodeError(Exception ex, string node);
}
