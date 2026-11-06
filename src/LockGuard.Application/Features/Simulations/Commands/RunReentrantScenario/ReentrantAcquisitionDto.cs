namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

/// <param name="IsReentrant"><c>false</c> only for the acquisition that actually reached the store.</param>
public sealed record ReentrantAcquisitionDto(int Level, bool IsReentrant, long FencingToken);
