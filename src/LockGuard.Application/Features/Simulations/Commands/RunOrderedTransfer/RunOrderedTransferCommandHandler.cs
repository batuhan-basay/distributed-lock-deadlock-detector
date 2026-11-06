using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Exceptions;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;

public sealed class RunOrderedTransferCommandHandler(IDistributedLockManager locks, TimeProvider time)
    : IRequestHandler<RunOrderedTransferCommand, TransferResponse>
{
    private const string Scenario = "scenario-safe";

    public async Task<TransferResponse> Handle(RunOrderedTransferCommand request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            SessionId = request.SessionId,
            Name = Scenario,
        }, cancellationToken);

        try
        {
            var handles = await session.AcquireOrderedAsync([request.FirstResource, request.SecondResource], cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(request.WorkMs), time, session.LockLost);
            await session.EnsureLocksHeldAsync(cancellationToken);

            return new TransferResponse(
                ScenarioOutcome.Completed,
                Scenario,
                session.SessionId,
                Attempts: 1,
                PreviousAttempts: [],
                LockOrder: [.. handles.Select(h => new AcquiredLockDto(h.Resource, h.FencingToken))],
                ElapsedMs: Elapsed(started));
        }
        catch (LockAcquisitionTimeoutException ex)
        {
            return Failure(ScenarioOutcome.Timeout, ex.Message, ex.HolderSessionId);
        }
        catch (Exception ex) when (ex is LockLostException || (ex is OperationCanceledException && session.LockLost.IsCancellationRequested))
        {
            return Failure(ScenarioOutcome.LockLost, session.LockLostReason ?? ex.Message, holder: null);
        }

        TransferResponse Failure(ScenarioOutcome outcome, string detail, string? holder) =>
            new(outcome, Scenario, session.SessionId, 1, [], [], Elapsed(started), detail, holder);
    }

    private long Elapsed(long started) => (long)time.GetElapsedTime(started).TotalMilliseconds;
}
