using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ReclaimStaleLocks;

/// <summary>One orphaned / stale lock reclamation pass. Returns the reports of the locks that were released.</summary>
public sealed record ReclaimStaleLocksCommand : IRequest<IReadOnlyList<LockEventReport>>;
