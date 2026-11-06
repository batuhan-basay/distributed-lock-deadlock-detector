using LockGuard.Application.Common.Options;
using LockGuard.Application.Locking.Watchdog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Infrastructure.BackgroundJobs;

/// <summary>Drives <see cref="LockWatchdog.RenewDueLeasesAsync"/> every <see cref="WatchdogOptions.TickInterval"/>.</summary>
public sealed partial class LockWatchdogHostedService(
    LockWatchdog watchdog,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<LockWatchdogHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.Watchdog;
        if (!settings.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(settings.TickInterval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await watchdog.RenewDueLeasesAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogLoopError(ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lock watchdog is disabled; leases will simply expire after their TTL")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Watchdog loop iteration failed")]
    private partial void LogLoopError(Exception ex);
}
