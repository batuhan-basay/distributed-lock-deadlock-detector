using System.Diagnostics;
using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Exceptions;
using LockGuard.Domain.Locks;
using LockGuard.Domain.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Locking;

/// <summary>
/// Default <see cref="IDistributedLockManager"/>. It owns the acquisition loop:
/// <list type="number">
///   <item>try to acquire on the store (Redlock quorum);</item>
///   <item>on contention, publish a <see cref="WaitRecord"/> (edge of the Wait-For Graph) and check for an abort signal;</item>
///   <item>back off exponentially with jitter until the wait timeout expires.</item>
/// </list>
/// </summary>
public sealed partial class DistributedLockManager : IDistributedLockManager
{
    private readonly ILockStore _store;
    private readonly ICoordinationStore _coordination;
    private readonly LockWatchdog _watchdog;
    private readonly LockGuardTelemetry _telemetry;
    private readonly TimeProvider _time;
    private readonly LockGuardOptions _options;
    private readonly ILogger<DistributedLockManager> _logger;

    public DistributedLockManager(
        ILockStore store,
        ICoordinationStore coordination,
        LockWatchdog watchdog,
        LockGuardTelemetry telemetry,
        TimeProvider time,
        IOptions<LockGuardOptions> options,
        ILogger<DistributedLockManager> logger)
    {
        _store = store;
        _coordination = coordination;
        _watchdog = watchdog;
        _telemetry = telemetry;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    internal ILockStore Store => _store;
    internal ICoordinationStore Coordination => _coordination;
    internal LockWatchdog Watchdog => _watchdog;
    internal LockGuardTelemetry Telemetry => _telemetry;
    internal TimeProvider Time => _time;
    internal LockGuardOptions Options => _options;

    public async Task<LockSession> BeginSessionAsync(LockSessionOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new LockSessionOptions();
        var sessionId = string.IsNullOrWhiteSpace(options.SessionId) ? $"s-{Guid.NewGuid():N}" : options.SessionId!;

        var info = new SessionInfo(
            sessionId,
            _options.ServiceName,
            _options.InstanceId,
            options.Name,
            options.Priority,
            _time.GetUtcNow());

        // A previous incarnation of an idempotent session may have been aborted; start clean.
        await _coordination.ClearAbortSignalAsync(sessionId, cancellationToken);
        await _coordination.RegisterSessionAsync(info, cancellationToken);

        return new LockSession(this, info, options);
    }

    /// <summary>The acquisition loop. Re-entrancy inside a session is handled by <see cref="LockSession"/> before we get here.</summary>
    internal async Task<AcquireResult> AcquireCoreAsync(LockSession session, string resource, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        var group = new KeyValuePair<string, object?>("resource_group", ResourceGroup.Of(resource));
        using var activity = LockGuardTelemetry.ActivitySource.StartActivity("lockguard.acquire", ActivityKind.Internal);
        activity?.SetTag("lockguard.resource", resource);
        activity?.SetTag("lockguard.session", session.SessionId);

        var owner = new LockOwner(session.SessionId, _options.ServiceName, _options.InstanceId);
        var leaseTime = session.LeaseTime;
        var waitTimeout = session.WaitTimeout;
        var started = _time.GetTimestamp();
        var attempt = 0;
        var waiting = false;
        DateTimeOffset waitingSince = default;
        string? holder = null;
        var outcome = "acquired";

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _store.TryAcquireAsync(resource, owner, leaseTime, cancellationToken);
                if (result.IsSuccess)
                {
                    outcome = result.Status == AcquireStatus.AlreadyOwned ? "already_owned" : "acquired";
                    activity?.SetTag("lockguard.fencing_token", result.FencingToken);
                    activity?.SetTag("lockguard.attempts", attempt + 1);
                    if (waiting)
                    {
                        LogAcquiredAfterWait(session.SessionId, resource, _time.GetElapsedTime(started).TotalMilliseconds, attempt + 1);
                    }

                    return result;
                }

                // ---- contended ---------------------------------------------------------------
                holder = result.CurrentHolderSessionId;
                _telemetry.ContentionEvents.Add(1, group);

                var now = _time.GetUtcNow();
                if (!waiting)
                {
                    waiting = true;
                    waitingSince = now;
                    LogContended(session.SessionId, resource, holder);
                }

                // Publish/refresh our edge in the Wait-For Graph: session --waits for--> resource (held by holder).
                await _coordination.RecordWaitAsync(
                    new WaitRecord(session.SessionId, resource, holder, _options.ServiceName, _options.InstanceId, waitingSince, now),
                    cancellationToken);

                // Did the detector choose us as a deadlock victim?
                var abort = await _coordination.GetAbortSignalAsync(session.SessionId, cancellationToken);
                if (abort is not null)
                {
                    outcome = abort.Reason == AbortReason.DeadlockVictim ? "deadlock_victim" : "aborted";
                    activity?.SetStatus(ActivityStatusCode.Error, outcome);
                    throw abort.Reason == AbortReason.DeadlockVictim
                        ? new DeadlockVictimException(resource, session.SessionId, abort)
                        : new LockLostException(resource, session.SessionId, abort.Details);
                }

                var elapsed = _time.GetElapsedTime(started);
                if (elapsed >= waitTimeout)
                {
                    outcome = "timeout";
                    activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                    throw new LockAcquisitionTimeoutException(resource, session.SessionId, holder, elapsed);
                }

                var delay = NextDelay(attempt++);
                var remaining = waitTimeout - elapsed;
                await Task.Delay(delay < remaining ? delay : remaining, _time, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            _telemetry.WaitDuration.Record(_time.GetElapsedTime(started).TotalSeconds, group, new("outcome", outcome));
            _telemetry.Acquisitions.Add(1, group, new("outcome", outcome));

            if (waiting)
            {
                try
                {
                    await _coordination.ClearWaitAsync(session.SessionId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Advisory data only: a leftover wait record expires through WaitRecordStaleAfter.
                    LogClearWaitFailed(ex, session.SessionId);
                }
            }
        }
    }

    /// <summary>Exponential backoff with "equal jitter": half deterministic, half random.</summary>
    private TimeSpan NextDelay(int attempt)
    {
        var initial = _options.Acquisition.InitialRetryDelay.TotalMilliseconds;
        var max = _options.Acquisition.MaxRetryDelay.TotalMilliseconds;
        var cap = Math.Min(max, initial * Math.Pow(2, Math.Min(attempt, 16)));
        return TimeSpan.FromMilliseconds((cap / 2) + (Random.Shared.NextDouble() * cap / 2));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session {SessionId} is waiting for {Resource} held by {Holder}")]
    private partial void LogContended(string sessionId, string resource, string? holder);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session {SessionId} acquired {Resource} after {ElapsedMs:F0} ms ({Attempts} attempts)")]
    private partial void LogAcquiredAfterWait(string sessionId, string resource, double elapsedMs, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not clear wait record of session {SessionId}")]
    private partial void LogClearWaitFailed(Exception ex, string sessionId);
}
