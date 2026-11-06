using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Domain.Coordination;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLocks;

public sealed class GetLocksQueryHandler(ILockStore store, TimeProvider time)
    : IRequestHandler<GetLocksQuery, IReadOnlyList<LockDto>>
{
    public async Task<IReadOnlyList<LockDto>> Handle(GetLocksQuery request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var locks = await store.ListAsync(cancellationToken);

        return [.. locks
            .Where(l => request.IncludeInternal || !InternalResources.IsInternal(l.Resource))
            .Where(l => string.IsNullOrEmpty(request.ResourcePrefix) || l.Resource.StartsWith(request.ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(l => l.Resource, StringComparer.Ordinal)
            .Select(l => new LockDto(
                l.Resource,
                l.SessionId,
                l.ServiceName,
                l.InstanceId,
                l.FencingToken,
                (long)l.HeldFor(now).TotalMilliseconds,
                l.RemainingTtl is { } ttl ? (long)ttl.TotalMilliseconds : null,
                InternalResources.IsInternal(l.Resource)))];
    }
}
