using System.Collections.Concurrent;
using System.Diagnostics;
using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Domain.Locks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Locking.Watchdog;

/// <summary>
/// Lock watchdog (lease auto-extension). Every held lock is written with a short TTL so that it disappears quickly if
/// this process crashes. While the work is still running, <see cref="RenewDueLeasesAsync"/> extends the TTL every
/// <c>LeaseTime * RenewalRatio</c>. When an extension fails (the lock expired or was reclaimed) the owner is notified
/// through <see cref="LeaseRegistration.LostToken"/>.
/// <para>
/// This class holds the lease registry and the renewal logic only; the periodic loop that drives it lives in the
/// Infrastructure layer (<c>LockWatchdogHostedService</c>).
/// </para>
/// </summary>
public sealed partial class LockWatchdog
{
    private readonly ConcurrentDictionary<(string Resource, string SessionId), Lease> _leases = new();
    private readonly ILockStore _store;
    private readonly LockGuardTelemetry _telemetry;
    private readonly TimeProvider _time;
    private readonly WatchdogOptions _options;
    private readonly ILogger<LockWatchdog> _logger;

    public LockWatchdog(
        ILockStore store,
        LockGuardTelemetry telemetry,
        TimeProvider time,
        IOptions<LockGuardOptions> options,
        ILogger<LockWatchdog> logger)
    {
        _store = store;
        _telemetry = telemetry;
        _time = time;
        _options = options.Value.Watchdog;
        _logger = logger;
    }

    public int ActiveLeases => _leases.Count;

    /// <summary>Starts tracking a lease; <paramref name="onLost"/> is invoked once if the lease cannot be renewed.</summary>
    public LeaseRegistration Track(string resource, string sessionId, TimeSpan leaseTime, Action<string>? onLost = null)
    {
        var lease = new Lease(resource, sessionId, leaseTime, _time.GetUtcNow(), onLost);
        var existing = _leases.GetOrAdd((resource, sessionId), lease);
        return new LeaseRegistration(existing.LostCts.Token);
    }

    public void Untrack(string resource, string sessionId) =>
        // The CancellationTokenSource is intentionally not disposed: callers may still hold its token.
        _leases.TryRemove((resource, sessionId), out _);

    /// <summary>Renews every lease that is due.</summary>
    /// <returns>Number of leases successfully renewed.</returns>
    public async Task<int> RenewDueLeasesAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var due = _leases.Values
            .Where(l => now - l.LastRenewedAt >= l.LeaseTime * _options.RenewalRatio)
            .ToList();

        if (due.Count == 0)
        {
            return 0;
        }

        var results = await Task.WhenAll(due.Select(l => RenewAsync(l, now, cancellationToken)));
        return results.Count(r => r);
    }

    private async Task<bool> RenewAsync(Lease lease, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var activity = LockGuardTelemetry.ActivitySource.StartActivity("lockguard.watchdog.extend");
        activity?.SetTag("lockguard.resource", lease.Resource);
        activity?.SetTag("lockguard.session", lease.SessionId);
        var group = new KeyValuePair<string, object?>("resource_group", ResourceGroup.Of(lease.Resource));

        try
        {
            var extended = await _store.ExtendAsync(lease.Resource, lease.SessionId, lease.LeaseTime, cancellationToken);
            if (extended)
            {
                lease.LastRenewedAt = now;
                _telemetry.WatchdogExtensions.Add(1, group, new("result", "extended"));
                return true;
            }

            _telemetry.WatchdogExtensions.Add(1, group, new("result", "lost"));
            MarkLost(lease, "lease could not be extended: lock expired or was reclaimed by another party");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _telemetry.WatchdogExtensions.Add(1, group, new("result", "error"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogRenewalError(ex, lease.Resource, lease.SessionId);

            // If we could not renew for a whole lease period, the lock has certainly expired in the store.
            if (_time.GetUtcNow() - lease.LastRenewedAt >= lease.LeaseTime)
            {
                MarkLost(lease, $"lease expired while the store was unreachable ({ex.GetType().Name})");
            }

            return false;
        }
    }

    private void MarkLost(Lease lease, string reason)
    {
        if (_leases.TryRemove((lease.Resource, lease.SessionId), out _))
        {
            LogLeaseLost(lease.Resource, lease.SessionId, reason);
            lease.SignalLost(reason);
        }
    }

    private sealed class Lease(string resource, string sessionId, TimeSpan leaseTime, DateTimeOffset renewedAt, Action<string>? onLost)
    {
        public string Resource { get; } = resource;

        public string SessionId { get; } = sessionId;

        public TimeSpan LeaseTime { get; } = leaseTime;

        public DateTimeOffset LastRenewedAt { get; set; } = renewedAt;

        public CancellationTokenSource LostCts { get; } = new();

        public void SignalLost(string reason)
        {
            try
            {
                onLost?.Invoke(reason);
            }
            finally
            {
                LostCts.Cancel();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Watchdog lost lease on {Resource} for session {SessionId}: {Reason}")]
    private partial void LogLeaseLost(string resource, string sessionId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Watchdog failed to renew {Resource} for session {SessionId}")]
    private partial void LogRenewalError(Exception ex, string resource, string sessionId);
}
