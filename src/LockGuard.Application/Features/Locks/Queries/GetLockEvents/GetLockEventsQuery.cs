using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLockEvents;

public sealed record GetLockEventsQuery(int Count = 50, LockEventKind? Kind = null) : IRequest<IReadOnlyList<LockEventReport>>
{
    public const int MaxCount = 200;
}
