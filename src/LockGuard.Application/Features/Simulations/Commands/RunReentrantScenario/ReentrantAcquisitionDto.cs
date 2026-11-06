namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

public sealed record ReentrantAcquisitionDto(int Level, bool IsReentrant, long FencingToken);
