using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLockEvents;

/// <summary>Recent detection / resolution / reclamation reports, newest first.</summary>
public sealed record GetLockEventsQuery(int Count = 50, LockEventKind? Kind = null) : IRequest<IReadOnlyList<LockEventReport>>
{
    public const int MaxCount = 200;
}
