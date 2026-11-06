namespace LockGuard.Application.Features.Simulations.Common;

public sealed record AcquiredLockDto(string Resource, long FencingToken);
