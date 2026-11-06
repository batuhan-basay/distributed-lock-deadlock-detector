using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Infrastructure.BackgroundJobs;

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
        try
        {
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await coordination.RemoveInstanceAsync(_options.InstanceId, cancellationToken);
        }
        catch
        {
            // best effort on shutdown
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance {InstanceId} registered (heartbeat started)")]
    private partial void LogStarted(string instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat failed")]
    private partial void LogHeartbeatFailed(Exception ex);
}
