using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetInstances;

/// <summary>Process instances and the age of their last heartbeat.</summary>
public sealed record GetInstancesQuery : IRequest<IReadOnlyList<InstanceDto>>;
