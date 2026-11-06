using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Exceptions;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed class RunThreeWayDeadlockCommandHandler(IDistributedLockManager locks, TimeProvider time)
    : IRequestHandler<RunThreeWayDeadlockCommand, ThreeWayDeadlockResponse>
{
    public async Task<ThreeWayDeadlockResponse> Handle(RunThreeWayDeadlockCommand request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var resources = RunThreeWayDeadlockCommand.Resources;

        var workers = await Task.WhenAll(Enumerable.Range(0, resources.Count)
            .Select(i => RunWorkerAsync(i, resources[i], resources[(i + 1) % resources.Count])));

        return new ThreeWayDeadlockResponse(
            Victims: workers.Count(w => w.Outcome == ScenarioOutcome.DeadlockVictim),
            Completed: workers.Count(w => w.Outcome == ScenarioOutcome.Completed),
            Workers: workers);

        async Task<ThreeWayWorkerResult> RunWorkerAsync(int index, string mine, string next)
        {
            var path = $"{mine} -> {next}";
            await using var session = await locks.BeginSessionAsync(new LockSessionOptions { Name = $"three-way-{index + 1}" }, cancellationToken);
            try
            {
                await session.AcquireAsync(mine, cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(request.HoldMs), time, cancellationToken);
                await session.AcquireAsync(next, cancellationToken);
                return new ThreeWayWorkerResult(index + 1, session.SessionId, path, ScenarioOutcome.Completed, Elapsed());
            }
            catch (DeadlockVictimException ex)
            {
                return new ThreeWayWorkerResult(index + 1, session.SessionId, path, ScenarioOutcome.DeadlockVictim, Elapsed(), ex.Signal.Details);
            }
            catch (LockAcquisitionTimeoutException ex)
            {
                return new ThreeWayWorkerResult(index + 1, session.SessionId, path, ScenarioOutcome.Timeout, Elapsed(), ex.Message);
            }
        }

        long Elapsed() => (long)time.GetElapsedTime(started).TotalMilliseconds;
    }
}
