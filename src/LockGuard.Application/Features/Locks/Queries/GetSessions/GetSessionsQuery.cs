using LockGuard.Domain.Sessions;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetSessions;

public sealed record GetSessionsQuery : IRequest<IReadOnlyList<SessionInfo>>;
