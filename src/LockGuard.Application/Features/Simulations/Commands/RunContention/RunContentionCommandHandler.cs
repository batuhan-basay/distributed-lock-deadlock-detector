using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Exceptions;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunContention;

public sealed class RunContentionCommandHandler(IDistributedLockManager locks, TimeProvider time)
    : IRequestHandler<RunContentionCommand, ContentionResponse>
{
    public async Task<ContentionResponse> Handle(RunContentionCommand request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            SessionId = request.SessionId,
            Name = "contention",
            WaitTimeout = TimeSpan.FromMilliseconds(request.WaitTimeoutMs),
        }, cancellationToken);

        try
        {
            await using var handle = await session.AcquireAsync(request.Resource, cancellationToken);
            var waited = Elapsed();
            await Task.Delay(TimeSpan.FromMilliseconds(request.WorkMs), time, session.LockLost);

            return new ContentionResponse(ScenarioOutcome.Completed, request.Resource, session.SessionId, waited, Elapsed(), handle.FencingToken);
        }
        catch (LockAcquisitionTimeoutException ex)
        {
            return new ContentionResponse(ScenarioOutcome.Timeout, request.Resource, session.SessionId,
                (long)ex.Waited.TotalMilliseconds, Elapsed(), HolderSessionId: ex.HolderSessionId);
        }

        long Elapsed() => (long)time.GetElapsedTime(started).TotalMilliseconds;
    }
}
