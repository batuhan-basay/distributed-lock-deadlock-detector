using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ReclaimStaleLocks;

public sealed record ReclaimStaleLocksCommand : IRequest<IReadOnlyList<LockEventReport>>;
