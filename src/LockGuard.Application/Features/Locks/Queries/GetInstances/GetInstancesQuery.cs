using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetInstances;

public sealed record GetInstancesQuery : IRequest<IReadOnlyList<InstanceDto>>;
