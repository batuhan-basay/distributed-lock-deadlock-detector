using LockGuard.Core.Abstractions;
using LockGuard.Core.Coordination;
using LockGuard.Core.Detection;
using LockGuard.Core.Reclaim;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Core.Hosting;

/// <summary>Publishes this process' liveness. The reclaimer treats locks of instances without a fresh heartbeat as orphaned.</summary>
public sealed partial class InstanceHeartbeatService(
    ICoordinationStore coordination,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<InstanceHeartbeatService> logger) : BackgroundService
{
    private readonly LockGuardOptions _options = options.Value;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // First heartbeat before the app starts serving requests, so no lock is ever taken by an "unknown" instance.
        await coordination.HeartbeatAsync(_options.InstanceId, time.GetUtcNow(), cancellationToken);
        LogStarted(_options.InstanceId);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Heartbeat.Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await coordination.HeartbeatAsync(_options.InstanceId, time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogHeartbeatFailed(ex);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            // Graceful shutdown: our locks were released by their sessions; leaving the entry would only delay nothing.
            await coordination.RemoveInstanceAsync(_options.InstanceId, cancellationToken);
        }
        catch
        {
            // ignored
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance {InstanceId} registered (heartbeat started)")]
    private partial void LogStarted(string instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception ex);
}

/// <summary>Runs <see cref="ScanOnceAsync"/> periodically, but only while this instance holds the leader lease for its role.</summary>
public abstract partial class LeaderElectedLoopService : BackgroundService
{
    private readonly LeaderElection _election;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly string _role;

    protected LeaderElectedLoopService(ILockStore store, TimeProvider time, LockGuardOptions options, string role, TimeSpan interval, ILogger logger)
    {
        _time = time;
        _logger = logger;
        _role = role;
        Interval = interval;
        var lease = TimeSpan.FromTicks(Math.Max(interval.Ticks * 5, TimeSpan.FromSeconds(5).Ticks));
        _election = new LeaderElection(store, role, options.ServiceName, options.InstanceId, lease);
    }

    protected TimeSpan Interval { get; }

    protected abstract bool Enabled { get; }

    public bool IsLeader => _election.IsLeader;

    protected abstract Task ScanOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            LogDisabled(_role);
            return;
        }

        var wasLeader = false;
        using var timer = new PeriodicTimer(Interval, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var leader = await _election.TryHoldAsync(stoppingToken);
                    if (leader != wasLeader)
                    {
                        LogLeadershipChanged(_role, leader);
                        wasLeader = leader;
                    }

                    if (leader)
                    {
                        await ScanOnceAsync(stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogScanFailed(ex, _role);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _election.ResignAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} is disabled on this instance")]
    private partial void LogDisabled(string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} leadership changed: leader={IsLeader}")]
    private partial void LogLeadershipChanged(string role, bool isLeader);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Role} scan failed")]
    private partial void LogScanFailed(Exception ex, string role);
}

/// <summary>Background worker that runs the deadlock detector (Wait-For Graph cycle detection + resolution).</summary>
public sealed class DeadlockDetectorService(
    DeadlockDetector detector,
    ILockStore store,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<DeadlockDetectorService> logger)
    : LeaderElectedLoopService(store, time, options.Value, "deadlock-detector", options.Value.Detection.ScanInterval, logger)
{
    protected override bool Enabled => options.Value.Detection.Enabled;

    protected override Task ScanOnceAsync(CancellationToken cancellationToken) => detector.ScanAsync(cancellationToken);
}

/// <summary>Background worker that releases orphaned / stale locks.</summary>
public sealed class OrphanedLockReclaimerService(
    OrphanedLockReclaimer reclaimer,
    ILockStore store,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<OrphanedLockReclaimerService> logger)
    : LeaderElectedLoopService(store, time, options.Value, "orphan-reclaimer", options.Value.Reclaimer.ScanInterval, logger)
{
    protected override bool Enabled => options.Value.Reclaimer.Enabled;

    protected override Task ScanOnceAsync(CancellationToken cancellationToken) => reclaimer.ScanAsync(cancellationToken);
}
