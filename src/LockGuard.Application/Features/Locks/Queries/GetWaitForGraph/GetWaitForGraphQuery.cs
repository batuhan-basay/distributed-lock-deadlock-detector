using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

/// <summary>Builds the current cluster-wide Wait-For Graph without acting on it.</summary>
public sealed record GetWaitForGraphQuery : IRequest<WaitForGraphResponse>;
