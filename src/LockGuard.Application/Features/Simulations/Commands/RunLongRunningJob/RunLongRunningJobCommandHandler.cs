using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;

public sealed class RunLongRunningJobCommandHandler(IDistributedLockManager locks, IOptions<LockGuardOptions> options, TimeProvider time)
    : IRequestHandler<RunLongRunningJobCommand, LongRunningJobResponse>
{
    public async Task<LongRunningJobResponse> Handle(RunLongRunningJobCommand request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var watchdog = options.Value.Watchdog;
        var watchdogEnabled = !request.DisableWatchdog && watchdog.Enabled;
        var expectedExtensions = watchdogEnabled ? (int)(request.WorkMs / (request.LeaseMs * watchdog.RenewalRatio)) : 0;

        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            Name = "long-running",
            LeaseTime = TimeSpan.FromMilliseconds(request.LeaseMs),
            DisableWatchdog = request.DisableWatchdog,
        }, cancellationToken);

        await using var handle = await session.AcquireAsync(request.Resource, cancellationToken);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(request.WorkMs), time, session.LockLost);
            await session.EnsureLocksHeldAsync(cancellationToken);

            return Response(ScenarioOutcome.Completed);
        }
        catch (Exception ex) when (ex is LockLostException || (ex is OperationCanceledException && session.LockLost.IsCancellationRequested))
        {
            return Response(
                ScenarioOutcome.LockLost,
                session.LockLostReason ?? ex.Message,
                request.DisableWatchdog ? "watchdog disabled: the lease expired while the work was still running" : null);
        }

        LongRunningJobResponse Response(ScenarioOutcome outcome, string? reason = null, string? hint = null) => new(
            outcome, request.Resource, session.SessionId, request.LeaseMs, request.WorkMs, watchdogEnabled, expectedExtensions,
            (long)time.GetElapsedTime(started).TotalMilliseconds, reason, hint);
    }
}
