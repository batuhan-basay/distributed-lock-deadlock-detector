using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLockEvents;

public sealed class GetLockEventsQueryHandler(ICoordinationStore coordination)
    : IRequestHandler<GetLockEventsQuery, IReadOnlyList<LockEventReport>>
{
    public async Task<IReadOnlyList<LockEventReport>> Handle(GetLockEventsQuery request, CancellationToken cancellationToken)
    {
        
        var events = await coordination.GetRecentEventsAsync(
            request.Kind is null ? request.Count : GetLockEventsQuery.MaxCount, cancellationToken);

        return request.Kind is null
            ? events
            : [.. events.Where(e => e.Kind == request.Kind).Take(request.Count)];
    }
}
