using System.Text.Json;
using System.Text.Json.Serialization;
using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Events;
using LockGuard.Domain.Sessions;
using StackExchange.Redis;

namespace LockGuard.Infrastructure.Persistence.Redis;

/// <summary>
/// Advisory coordination data on a single Redis node:
/// <code>
/// {prefix}sessions   HASH  sessionId  -> SessionInfo json
/// {prefix}waits      HASH  sessionId  -> WaitRecord json      (edges of the Wait-For Graph)
/// {prefix}instances  HASH  instanceId -> last heartbeat (unix ms)
/// {prefix}abort:{id} STRING AbortSignal json (PX ttl)
/// {prefix}events     LIST  LockEventReport json (newest first, capped)
/// </code>
/// </summary>
public sealed class RedisCoordinationStore : ICoordinationStore
{
    private const int MaxEvents = 200;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly RedisNodePool _pool;
    private readonly RedisKey _sessions;
    private readonly RedisKey _waits;
    private readonly RedisKey _instances;
    private readonly RedisKey _events;
    private readonly string _abortPrefix;

    public RedisCoordinationStore(RedisNodePool pool)
    {
        _pool = pool;
        var p = pool.Options.KeyPrefix;
        _sessions = p + "sessions";
        _waits = p + "waits";
        _instances = p + "instances";
        _events = p + "events";
        _abortPrefix = p + "abort:";
    }

    private IDatabase Db => _pool.CoordinationDatabase;

    public Task RegisterSessionAsync(SessionInfo session, CancellationToken cancellationToken = default) =>
        Db.HashSetAsync(_sessions, session.SessionId, Serialize(session));

    public Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Db.HashDeleteAsync(_sessions, sessionId);

    public async Task<IReadOnlyDictionary<string, SessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await Db.HashGetAllAsync(_sessions);
        var result = new Dictionary<string, SessionInfo>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (Deserialize<SessionInfo>(entry.Value) is { } session)
            {
                result[session.SessionId] = session;
            }
        }

        return result;
    }

    public Task RecordWaitAsync(WaitRecord wait, CancellationToken cancellationToken = default) =>
        Db.HashSetAsync(_waits, wait.WaiterSessionId, Serialize(wait));

    public Task ClearWaitAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Db.HashDeleteAsync(_waits, sessionId);

    public async Task<IReadOnlyList<WaitRecord>> GetWaitsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await Db.HashGetAllAsync(_waits);
        return [.. entries.Select(e => Deserialize<WaitRecord>(e.Value)).Where(w => w is not null).Select(w => w!)];
    }

    public Task HeartbeatAsync(string instanceId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Db.HashSetAsync(_instances, instanceId, now.ToUnixTimeMilliseconds());

    public Task RemoveInstanceAsync(string instanceId, CancellationToken cancellationToken = default) =>
        Db.HashDeleteAsync(_instances, instanceId);

    public async Task<IReadOnlyDictionary<string, InstanceHeartbeat>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        var entries = await Db.HashGetAllAsync(_instances);
        var result = new Dictionary<string, InstanceHeartbeat>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Value.TryParse(out long ms))
            {
                var id = entry.Name.ToString();
                result[id] = new InstanceHeartbeat(id, DateTimeOffset.FromUnixTimeMilliseconds(ms));
            }
        }

        return result;
    }

    public Task SignalAbortAsync(AbortSignal signal, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Db.StringSetAsync(_abortPrefix + signal.SessionId, Serialize(signal), expiry: ttl, when: When.Always);

    public async Task<AbortSignal?> GetAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Deserialize<AbortSignal>(await Db.StringGetAsync(_abortPrefix + sessionId));

    public Task ClearAbortSignalAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Db.KeyDeleteAsync(_abortPrefix + sessionId);

    public async Task AppendEventAsync(LockEventReport report, CancellationToken cancellationToken = default)
    {
        var tx = Db.CreateTransaction();
        _ = tx.ListLeftPushAsync(_events, Serialize(report));
        _ = tx.ListTrimAsync(_events, 0, MaxEvents - 1);
        await tx.ExecuteAsync();
    }

    public async Task<IReadOnlyList<LockEventReport>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default)
    {
        var values = await Db.ListRangeAsync(_events, 0, Math.Max(0, count - 1));
        return [.. values.Select(Deserialize<LockEventReport>).Where(r => r is not null).Select(r => r!)];
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static T? Deserialize<T>(RedisValue value) where T : class
    {
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value.ToString(), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
