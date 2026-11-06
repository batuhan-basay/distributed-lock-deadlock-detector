using LockGuard.Core.Abstractions;
using LockGuard.Core.Locking;
using LockGuard.Core.Models;
using StackExchange.Redis;

namespace LockGuard.IntegrationTests;

public class RedlockIntegrationTests(RedlockClusterFixture cluster) : IClassFixture<RedlockClusterFixture>
{
    private static string Prefix() => $"it-{Guid.NewGuid():N}:";

    [Fact]
    public async Task Acquire_is_idempotent_per_session_and_exclusive_across_sessions()
    {
        await using var sp = cluster.CreateInstance(Prefix());
        var store = sp.Store;
        var owner = new LockOwner("s1", "svc", "i1");

        var first = await store.TryAcquireAsync("order:1", owner, TimeSpan.FromSeconds(10));
        var again = await store.TryAcquireAsync("order:1", owner with { InstanceId = "i2" }, TimeSpan.FromSeconds(10));
        var other = await store.TryAcquireAsync("order:1", new LockOwner("s2", "svc", "i3"), TimeSpan.FromSeconds(10));

        Assert.Equal(AcquireStatus.Acquired, first.Status);
        Assert.Equal(AcquireStatus.AlreadyOwned, again.Status);
        Assert.Equal(first.FencingToken, again.FencingToken);
        Assert.Equal(AcquireStatus.Contended, other.Status);
        Assert.Equal("s1", other.CurrentHolderSessionId);

        Assert.False(await store.ReleaseAsync("order:1", "s2")); // compare-and-delete: not the owner
        Assert.True(await store.ReleaseAsync("order:1", "s1"));
        Assert.Null(await store.GetAsync("order:1"));
    }

    [Fact]
    public async Task Lock_is_written_on_every_node_and_list_returns_the_quorum_view()
    {
        var prefix = Prefix();
        await using var sp = cluster.CreateInstance(prefix);
        await using var session = await sp.Locks.BeginSessionAsync();
        await using var handle = await session.AcquireAsync("inventory:sku-1");

        foreach (var node in sp.Pool.Nodes)
        {
            Assert.Equal(session.SessionId, (string?)await node.GetDatabase().HashGetAsync($"{prefix}lock:{{inventory:sku-1}}", "owner"));
        }

        var info = Assert.Single(await sp.Store.ListAsync());
        Assert.Equal("inventory:sku-1", info.Resource);
        Assert.Equal(session.SessionId, info.SessionId);
        Assert.NotNull(info.RemainingTtl);
    }

    [Fact]
    public async Task Parallel_sessions_on_two_instances_never_overlap_in_the_critical_section()
    {
        var prefix = Prefix();
        await using var instance1 = cluster.CreateInstance(prefix, "api-1");
        await using var instance2 = cluster.CreateInstance(prefix, "api-2");
        var counterDb = instance1.Pool.Nodes[0].GetDatabase();
        var counterKey = $"{prefix}counter";
        var inside = 0;
        var maxInside = 0;

        await Task.WhenAll(Enumerable.Range(0, 30).Select(async i =>
        {
            var locks = (i % 2 == 0 ? instance1 : instance2).Locks;
            for (var n = 0; n < 5; n++)
            {
                await using var session = await locks.BeginSessionAsync();
                await using var handle = await session.AcquireAsync("hot-key");

                var now = Interlocked.Increment(ref inside);
                InterlockedMax(ref maxInside, now);

                // Non-atomic read-modify-write on Redis: correct only under mutual exclusion.
                var value = (long)await counterDb.StringGetAsync(counterKey);
                await Task.Delay(1);
                await counterDb.StringSetAsync(counterKey, value + 1);

                Interlocked.Decrement(ref inside);
            }
        }));

        Assert.Equal(150, (long)await counterDb.StringGetAsync(counterKey));
        Assert.Equal(1, maxInside);
    }

    [Fact]
    public async Task Opposite_order_sessions_on_different_instances_are_resolved_by_the_detector()
    {
        var prefix = Prefix();
        await using var api1 = cluster.CreateInstance(prefix, "api-1");
        await using var api2 = cluster.CreateInstance(prefix, "api-2");
        await using var detectorInstance = cluster.CreateInstance(prefix, "detector");

        using var cts = new CancellationTokenSource();
        var detectorLoop = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await detectorInstance.Detector.ScanAsync();
                await Task.Delay(100);
            }
        });

        var ready = new CountdownEvent(2);
        async Task<string> Transfer(IDistributedLockManager locks, string first, string second)
        {
            await using var session = await locks.BeginSessionAsync();
            await using var a = await session.AcquireAsync(first);
            ready.Signal();
            ready.Wait(TimeSpan.FromSeconds(5));
            try
            {
                await using var b = await session.AcquireAsync(second);
                return "completed";
            }
            catch (DeadlockVictimException)
            {
                return "victim";
            }
        }

        var results = await Task.WhenAll(
            Task.Run(() => Transfer(api1.Locks, "account:A", "account:B")),
            Task.Run(() => Transfer(api2.Locks, "account:B", "account:A")));

        await cts.CancelAsync();
        await detectorLoop;

        Assert.Contains("victim", results);
        Assert.Contains("completed", results);
        Assert.Contains(await api1.Coordination.GetRecentEventsAsync(10), e => e.Kind == LockEventKind.DeadlockDetected);
    }

    [Fact]
    public async Task Orphaned_lock_of_a_crashed_instance_is_reclaimed()
    {
        await using var sp = cluster.CreateInstance(Prefix(), configure: o => o.Reclaimer.MinLockAge = TimeSpan.Zero);
        var orphan = new LockOwner("s-orphan", "svc", "crashed-instance");
        Assert.True((await sp.Store.TryAcquireAsync("payment:9", orphan, TimeSpan.FromMinutes(10))).IsSuccess);

        var report = Assert.Single(await sp.Reclaimer.ScanAsync());

        Assert.Equal("payment:9", report.Resource);
        Assert.Null(await sp.Store.GetAsync("payment:9"));
        Assert.NotNull(await sp.Coordination.GetAbortSignalAsync("s-orphan"));
    }

    [Fact]
    public async Task Lock_without_expiry_written_by_a_legacy_client_is_detected_and_reclaimed()
    {
        var prefix = Prefix();
        await using var sp = cluster.CreateInstance(prefix, configure: o => o.Reclaimer.MinLockAge = TimeSpan.Zero);
        await sp.Coordination.HeartbeatAsync("legacy-1", DateTimeOffset.UtcNow);
        foreach (var node in sp.Pool.Nodes)
        {
            await node.GetDatabase().HashSetAsync($"{prefix}lock:{{legacy:1}}",
            [
                new HashEntry("owner", "s-legacy"), new HashEntry("service", "legacy"), new HashEntry("instance", "legacy-1"),
                new HashEntry("acquired", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), new HashEntry("fence", 0),
            ]);
        }

        var info = await sp.Store.GetAsync("legacy:1");
        Assert.NotNull(info);
        Assert.True(info.HasNoExpiry);

        var report = Assert.Single(await sp.Reclaimer.ScanAsync());
        Assert.Equal("no_expiry", report.Reason);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
