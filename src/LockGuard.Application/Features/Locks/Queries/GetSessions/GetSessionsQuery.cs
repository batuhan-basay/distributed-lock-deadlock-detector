using LockGuard.Domain.Sessions;
using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetSessions;

/// <summary>Lock sessions currently registered in the coordination store, oldest first.</summary>
public sealed record GetSessionsQuery : IRequest<IReadOnlyList<SessionInfo>>;
