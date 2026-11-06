using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

public sealed record GetWaitForGraphQuery : IRequest<WaitForGraphResponse>;
