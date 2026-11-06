using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Exceptions;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;

public sealed class RunDeadlockScenarioCommandHandler(IDistributedLockManager locks, TimeProvider time)
    : IRequestHandler<RunDeadlockScenarioCommand, TransferResponse>
{
    public async Task<TransferResponse> Handle(RunDeadlockScenarioCommand request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var previousAttempts = new List<FailedAttemptDto>();

        for (var attempt = 0; ; attempt++)
        {
            await using var session = await locks.BeginSessionAsync(new LockSessionOptions
            {
                SessionId = attempt == 0 ? request.SessionId : null,
                Name = $"{request.Scenario}#{attempt + 1}",
            }, cancellationToken);

            try
            {
                var first = await session.AcquireAsync(request.FirstResource, cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(request.HoldMs), time, cancellationToken); 
                var second = await session.AcquireAsync(request.SecondResource, cancellationToken);

                await Task.Delay(TimeSpan.FromMilliseconds(request.WorkMs), time, session.LockLost); 
                await session.EnsureLocksHeldAsync(cancellationToken);                              

                return new TransferResponse(
                    ScenarioOutcome.Completed,
                    request.Scenario,
                    session.SessionId,
                    attempt + 1,
                    previousAttempts,
                    [new(first.Resource, first.FencingToken), new(second.Resource, second.FencingToken)],
                    Elapsed(started));
            }
            catch (DeadlockVictimException ex) when (attempt < request.MaxRetries)
            {
                previousAttempts.Add(new FailedAttemptDto(session.SessionId, ScenarioOutcome.DeadlockVictim, ex.Signal.Details));

                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(400, 900) * (attempt + 1)), time, cancellationToken);
            }
            catch (DeadlockVictimException ex)
            {
                return Failure(ScenarioOutcome.DeadlockVictim, ex.Signal.Details, holder: null);
            }
            catch (LockAcquisitionTimeoutException ex)
            {
                return Failure(ScenarioOutcome.Timeout, ex.Message, ex.HolderSessionId);
            }
            catch (Exception ex) when (ex is LockLostException || (ex is OperationCanceledException && session.LockLost.IsCancellationRequested))
            {
                return Failure(ScenarioOutcome.LockLost, session.LockLostReason ?? ex.Message, holder: null);
            }

            TransferResponse Failure(ScenarioOutcome outcome, string detail, string? holder) => new(
                outcome, request.Scenario, session.SessionId, attempt + 1, previousAttempts, [], Elapsed(started), detail, holder);
        }
    }

    private long Elapsed(long started) => (long)time.GetElapsedTime(started).TotalMilliseconds;
}
