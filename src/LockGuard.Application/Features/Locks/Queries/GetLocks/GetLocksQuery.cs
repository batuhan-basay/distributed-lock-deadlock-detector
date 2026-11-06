using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLocks;

public sealed record GetLocksQuery(string? ResourcePrefix = null, bool IncludeInternal = true) : IRequest<IReadOnlyList<LockDto>>;
