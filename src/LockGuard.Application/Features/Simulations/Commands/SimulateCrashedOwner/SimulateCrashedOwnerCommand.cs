using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

/// <summary>
/// Writes a lock exactly like a real client would, but on behalf of an instance id that never sends a heartbeat
/// (= a process that died right after acquiring). Without the reclaimer the resource would be blocked for
/// <see cref="TtlSeconds"/>.
/// </summary>
public sealed record SimulateCrashedOwnerCommand(string Resource = "payment:tx-1001", int TtlSeconds = 600)
    : IRequest<CrashedOwnerResponse>;
