using LockGuard.Application.Detection;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

public sealed class GetWaitForGraphQueryHandler(DeadlockDetector detector)
    : IRequestHandler<GetWaitForGraphQuery, WaitForGraphResponse>
{
    public async Task<WaitForGraphResponse> Handle(GetWaitForGraphQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await detector.CaptureAsync(cancellationToken);
        return new WaitForGraphResponse(
            snapshot.CapturedAt,
            snapshot.IsDeadlocked,
            snapshot.Edges,
            [.. snapshot.Cycles.Select(c => new CycleDto(c.Sessions, c.Describe(), c.Edges))],
            WaitForGraphMermaidRenderer.Render(snapshot));
    }
}
