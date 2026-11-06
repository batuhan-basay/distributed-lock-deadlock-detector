using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Domain.Sessions;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetSessions;

public sealed class GetSessionsQueryHandler(ICoordinationStore coordination)
    : IRequestHandler<GetSessionsQuery, IReadOnlyList<SessionInfo>>
{
    public async Task<IReadOnlyList<SessionInfo>> Handle(GetSessionsQuery request, CancellationToken cancellationToken) =>
        [.. (await coordination.GetSessionsAsync(cancellationToken)).Values.OrderBy(s => s.StartedAt)];
}
