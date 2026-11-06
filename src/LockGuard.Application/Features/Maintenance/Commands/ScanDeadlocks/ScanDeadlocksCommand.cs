using LockGuard.Domain.Deadlocks;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ScanDeadlocks;

/// <summary>
/// One detection + resolution pass over the Wait-For Graph. Sent periodically by the leader-elected detector worker;
/// has side effects (abort signals, forced releases, reports), hence a command.
/// </summary>
public sealed record ScanDeadlocksCommand : IRequest<WaitForGraphSnapshot>;
