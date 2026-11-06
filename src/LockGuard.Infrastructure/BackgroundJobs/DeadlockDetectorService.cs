using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Maintenance.Commands.ScanDeadlocks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Infrastructure.BackgroundJobs;

/// <summary>Leader-elected worker that sends <see cref="ScanDeadlocksCommand"/> every <c>Detection:ScanInterval</c>.</summary>
public sealed class DeadlockDetectorService(
    IServiceScopeFactory scopes,
    ILockStore store,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<DeadlockDetectorService> logger)
    : LeaderElectedLoopService(scopes, store, time, options.Value, "deadlock-detector", options.Value.Detection.ScanInterval, logger)
{
    protected override bool Enabled => options.Value.Detection.Enabled;

    protected override Task SendAsync(ISender sender, CancellationToken cancellationToken) =>
        sender.Send(new ScanDeadlocksCommand(), cancellationToken);
}
