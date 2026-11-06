using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;
using LockGuard.Domain.Reclamation;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Reclamation;

public class OrphanedLockReclaimerTests
{
    private static LockGuardTestHost CreateHost(ManualTimeProvider time) => new(time, o =>
    {
        o.Reclaimer.InstanceStaleAfter = TimeSpan.FromSeconds(10);
        o.Reclaimer.MinLockAge = TimeSpan.FromSeconds(3);
        o.Reclaimer.MaxHoldTime = TimeSpan.FromMinutes(5);
    });

    [Fact]
    public async Task Lock_of_a_crashed_instance_is_reclaimed_and_its_session_is_signalled()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);

        await host.Coordination.HeartbeatAsync("api-1", time.GetUtcNow());
        host.Store.Seed("payment:1", new LockOwner("s-dead", "api", "api-1"), time.GetUtcNow(), TimeSpan.FromMinutes(10));

        time.Advance(TimeSpan.FromSeconds(15)); // api-1 stops sending heartbeats (crash)
        var reports = await host.Reclaimer.ScanAsync();

        var report = Assert.Single(reports);
        Assert.Equal("payment:1", report.Resource);
        Assert.Equal("owner_instance_dead", report.Reason);
        Assert.Null(await host.Store.GetAsync("payment:1"));

        var signal = await host.Coordination.GetAbortSignalAsync("s-dead");
        Assert.NotNull(signal);
        Assert.Equal(AbortReason.LockReclaimed, signal.Reason);
    }

    [Fact]
    public async Task Lock_of_a_never_seen_instance_is_reclaimed_after_min_age()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        host.Store.Seed("payment:2", new LockOwner("s-ghost", "api", "crashed-xyz"), time.GetUtcNow(), TimeSpan.FromMinutes(10));

        Assert.Empty(await host.Reclaimer.ScanAsync()); // too young: grace period protects fresh locks

        time.Advance(TimeSpan.FromSeconds(4));
        var report = Assert.Single(await host.Reclaimer.ScanAsync());
        Assert.Equal("owner_instance_unknown", report.Reason);
    }

    [Fact]
    public async Task Healthy_lock_is_left_alone()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        host.Store.Seed("order:1", new LockOwner("s-ok", "api", "api-1"), time.GetUtcNow(), TimeSpan.FromMinutes(1));

        time.Advance(TimeSpan.FromSeconds(30));
        await host.Coordination.HeartbeatAsync("api-1", time.GetUtcNow());

        Assert.Empty(await host.Reclaimer.ScanAsync());
        Assert.NotNull(await host.Store.GetAsync("order:1"));
    }

    [Fact]
    public async Task Lock_without_expiry_is_reclaimed_even_if_owner_is_alive()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        host.Store.Seed("legacy:1", new LockOwner("s-legacy", "batch", "batch-1"), time.GetUtcNow(), ttl: null);

        time.Advance(TimeSpan.FromSeconds(5));
        await host.Coordination.HeartbeatAsync("batch-1", time.GetUtcNow());

        var report = Assert.Single(await host.Reclaimer.ScanAsync());
        Assert.Equal("no_expiry", report.Reason);
    }

    [Fact]
    public async Task Zombie_lock_held_beyond_max_hold_time_is_reclaimed()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        var acquiredAt = time.GetUtcNow();
        host.Store.Seed("report:1", new LockOwner("s-zombie", "worker", "worker-1"), acquiredAt, TimeSpan.FromMinutes(30));

        time.Advance(TimeSpan.FromMinutes(6));
        await host.Coordination.HeartbeatAsync("worker-1", time.GetUtcNow()); // process alive, but stuck holding the lock

        var report = Assert.Single(await host.Reclaimer.ScanAsync());
        Assert.Equal("max_hold_time_exceeded", report.Reason);
    }

    [Fact]
    public async Task Internal_leader_lock_is_never_reclaimed()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        host.Store.Seed(InternalResources.Leader("deadlock-detector"), new LockOwner("leader", "detector", "gone"), time.GetUtcNow(), TimeSpan.FromHours(1));

        time.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty(await host.Reclaimer.ScanAsync());
    }

    [Fact]
    public async Task Reclaim_is_compare_and_delete_and_never_steals_a_reacquired_lock()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        var stale = new LockInfo("res", "s-old", "api", "dead", time.GetUtcNow(), TimeSpan.FromMinutes(1), 1);

        // Between the scan and the release the stale lock expired and a healthy session re-acquired the resource.
        host.Store.Seed("res", new LockOwner("s-new", "api", "alive"), time.GetUtcNow(), TimeSpan.FromMinutes(1));

        Assert.False(await host.Store.ReleaseAsync(stale.Resource, stale.SessionId));
        Assert.Equal("s-new", (await host.Store.GetAsync("res"))!.SessionId);
    }

    [Theory]
    [InlineData(StaleLockReason.OwnerInstanceDead, "owner_instance_dead")]
    [InlineData(StaleLockReason.NoExpiry, "no_expiry")]
    [InlineData(StaleLockReason.MaxHoldTimeExceeded, "max_hold_time_exceeded")]
    public void Reasons_map_to_stable_metric_tags(StaleLockReason reason, string tag) =>
        Assert.Equal(tag, StaleLockPolicy.ToTag(reason));
}
