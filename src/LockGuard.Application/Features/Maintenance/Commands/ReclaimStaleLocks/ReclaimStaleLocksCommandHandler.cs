using LockGuard.Application.Reclamation;
using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ReclaimStaleLocks;

public sealed class ReclaimStaleLocksCommandHandler(OrphanedLockReclaimer reclaimer)
    : IRequestHandler<ReclaimStaleLocksCommand, IReadOnlyList<LockEventReport>>
{
    public Task<IReadOnlyList<LockEventReport>> Handle(ReclaimStaleLocksCommand request, CancellationToken cancellationToken) =>
        reclaimer.ScanAsync(cancellationToken);
}
