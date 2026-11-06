namespace LockGuard.Application.Features.Simulations.Common;

/// <summary>A lock acquired during a simulation, in acquisition order.</summary>
public sealed record AcquiredLockDto(string Resource, long FencingToken);
