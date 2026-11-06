using MediatR;

namespace LockGuard.Application.Features.Locks.Queries.GetLocks;

/// <summary>Lists the locks currently held (quorum view of the lock store).</summary>
/// <param name="ResourcePrefix">Optional filter, e.g. <c>account:</c>.</param>
/// <param name="IncludeInternal">Include LockGuard's own leader-election locks.</param>
public sealed record GetLocksQuery(string? ResourcePrefix = null, bool IncludeInternal = true) : IRequest<IReadOnlyList<LockDto>>;
