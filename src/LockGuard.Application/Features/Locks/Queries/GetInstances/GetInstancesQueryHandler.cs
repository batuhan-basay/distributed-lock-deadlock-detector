using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Features.Locks.Queries.GetInstances;

public sealed class GetInstancesQueryHandler(ICoordinationStore coordination, IOptions<LockGuardOptions> options, TimeProvider time)
    : IRequestHandler<GetInstancesQuery, IReadOnlyList<InstanceDto>>
{
    public async Task<IReadOnlyList<InstanceDto>> Handle(GetInstancesQuery request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var staleAfter = options.Value.Reclaimer.InstanceStaleAfter;
        var instances = await coordination.GetInstancesAsync(cancellationToken);

        return [.. instances.Values
            .OrderBy(i => i.InstanceId, StringComparer.Ordinal)
            .Select(i => new InstanceDto(i.InstanceId, i.LastSeenAt, (now - i.LastSeenAt).TotalSeconds, now - i.LastSeenAt <= staleAfter))];
    }
}
