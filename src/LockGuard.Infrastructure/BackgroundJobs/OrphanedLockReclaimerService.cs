using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Maintenance.Commands.ReclaimStaleLocks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Infrastructure.BackgroundJobs;

public sealed class OrphanedLockReclaimerService(
    IServiceScopeFactory scopes,
    ILockStore store,
    TimeProvider time,
    IOptions<LockGuardOptions> options,
    ILogger<OrphanedLockReclaimerService> logger)
    : LeaderElectedLoopService(scopes, store, time, options.Value, "orphan-reclaimer", options.Value.Reclaimer.ScanInterval, logger)
{
    protected override bool Enabled => options.Value.Reclaimer.Enabled;

    protected override Task SendAsync(ISender sender, CancellationToken cancellationToken) =>
        sender.Send(new ReclaimStaleLocksCommand(), cancellationToken);
}
