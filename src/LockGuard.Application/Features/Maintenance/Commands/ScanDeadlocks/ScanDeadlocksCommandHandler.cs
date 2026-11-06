using LockGuard.Application.Detection;
using LockGuard.Domain.Deadlocks;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ScanDeadlocks;

public sealed class ScanDeadlocksCommandHandler(DeadlockDetector detector)
    : IRequestHandler<ScanDeadlocksCommand, WaitForGraphSnapshot>
{
    public Task<WaitForGraphSnapshot> Handle(ScanDeadlocksCommand request, CancellationToken cancellationToken) =>
        detector.ScanAsync(cancellationToken);
}
