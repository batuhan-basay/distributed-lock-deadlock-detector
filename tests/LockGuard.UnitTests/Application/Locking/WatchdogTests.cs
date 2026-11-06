using LockGuard.Application.Locking;
using LockGuard.Domain.Exceptions;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Locking;

public class WatchdogTests
{
    private static LockGuardTestHost CreateHost(ManualTimeProvider time) => new(time, o =>
    {
        o.Acquisition.LeaseTime = TimeSpan.FromSeconds(3);
        o.Watchdog.RenewalRatio = 1.0 / 3.0;
    });

    [Fact]
    public async Task Watchdog_keeps_extending_a_lease_while_the_work_runs()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        await using var session = await host.Manager.BeginSessionAsync();
        await using var handle = await session.AcquireAsync("report:monthly");

        // 30 seconds of "work" with a 3 second lease.
        var renewed = 0;
        for (var i = 0; i < 30; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1.01));
            renewed += await host.Watchdog.RenewDueLeasesAsync();
        }

        Assert.True(renewed >= 29, $"expected ~30 renewals, got {renewed}");
        var info = await host.Store.GetAsync("report:monthly");
        Assert.NotNull(info);
        Assert.Equal(session.SessionId, info.SessionId);
        Assert.False(session.LockLost.IsCancellationRequested);
    }

    [Fact]
    public async Task Without_renewal_the_lease_expires_after_its_ttl()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        await using var session = await host.Manager.BeginSessionAsync(new() { DisableWatchdog = true });
        await using var handle = await session.AcquireAsync("report:monthly");

        time.Advance(TimeSpan.FromSeconds(3.1));
        await host.Watchdog.RenewDueLeasesAsync();

        Assert.Null(await host.Store.GetAsync("report:monthly"));
        Assert.Equal(0, host.Watchdog.ActiveLeases);
    }

    [Fact]
    public async Task Commit_check_against_the_store_detects_an_expired_lease_when_watchdog_is_disabled()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        await using var session = await host.Manager.BeginSessionAsync(new() { DisableWatchdog = true });
        await using var handle = await session.AcquireAsync("report:daily");

        await session.EnsureLocksHeldAsync(); // still fine

        time.Advance(TimeSpan.FromSeconds(4)); // work took longer than the 3s lease
        await Assert.ThrowsAsync<LockLostException>(() => session.EnsureLocksHeldAsync());
        Assert.True(session.LockLost.IsCancellationRequested);
    }

    [Fact]
    public async Task Lost_lease_cancels_the_session_token_and_fails_the_commit_check()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        await using var session = await host.Manager.BeginSessionAsync();
        await using var handle = await session.AcquireAsync("payment:7");

        // Someone (e.g. the reclaimer) force-releases the lock behind the session's back.
        Assert.True(await host.Store.ReleaseAsync("payment:7", session.SessionId));

        time.Advance(TimeSpan.FromSeconds(1.1));
        await host.Watchdog.RenewDueLeasesAsync();

        Assert.True(session.LockLost.IsCancellationRequested);
        Assert.NotNull(session.LockLostReason);
        Assert.Throws<LockLostException>(session.EnsureLocksHeld);
    }

    [Fact]
    public async Task Released_locks_are_no_longer_renewed()
    {
        var time = new ManualTimeProvider();
        await using var host = CreateHost(time);
        await using var session = await host.Manager.BeginSessionAsync();
        var handle = await session.AcquireAsync("short");
        Assert.Equal(1, host.Watchdog.ActiveLeases);

        await handle.DisposeAsync();
        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(0, host.Watchdog.ActiveLeases);
        Assert.Equal(0, await host.Watchdog.RenewDueLeasesAsync());
    }
}
