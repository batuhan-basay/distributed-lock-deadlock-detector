using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Application.Locking;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

public sealed class RunReentrantScenarioCommandHandler(IDistributedLockManager locks, TimeProvider time)
    : IRequestHandler<RunReentrantScenarioCommand, ReentrantScenarioResponse>
{
    public async Task<ReentrantScenarioResponse> Handle(RunReentrantScenarioCommand request, CancellationToken cancellationToken)
    {
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            SessionId = request.SessionId,
            Name = "reentrant",
        }, cancellationToken);

        var handles = new List<LockHandle>(request.Depth);
        try
        {
            for (var i = 0; i < request.Depth; i++)
            {
                handles.Add(await session.AcquireAsync(request.Resource, cancellationToken));
            }

            var holdCount = session.HeldResources.GetValueOrDefault(request.Resource);
            if (request.HoldMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(request.HoldMs), time, cancellationToken);
            }

            return new ReentrantScenarioResponse(
                ScenarioOutcome.Completed,
                request.Resource,
                session.SessionId,
                holdCount,
                [.. handles.Select((h, i) => new ReentrantAcquisitionDto(i + 1, h.IsReentrant, h.FencingToken))]);
        }
        finally
        {
            
            for (var i = handles.Count - 1; i >= 0; i--)
            {
                await handles[i].DisposeAsync();
            }
        }
    }
}
