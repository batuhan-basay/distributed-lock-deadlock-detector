using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Coordination;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LockGuard.Infrastructure.BackgroundJobs;

public abstract partial class LeaderElectedLoopService : BackgroundService
{
    private readonly LeaderElection _election;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly string _role;

    protected LeaderElectedLoopService(
        IServiceScopeFactory scopes,
        ILockStore store,
        TimeProvider time,
        LockGuardOptions options,
        string role,
        TimeSpan interval,
        ILogger logger)
    {
        _scopes = scopes;
        _time = time;
        _logger = logger;
        _role = role;
        Interval = interval;
        var lease = TimeSpan.FromTicks(Math.Max(interval.Ticks * 5, TimeSpan.FromSeconds(5).Ticks));
        _election = new LeaderElection(store, role, options.ServiceName, options.InstanceId, lease);
    }

    protected TimeSpan Interval { get; }

    protected abstract bool Enabled { get; }

    public bool IsLeader => _election.IsLeader;

    public string Role => _role;

    protected abstract Task SendAsync(ISender sender, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            LogDisabled(_role);
            return;
        }

        var wasLeader = false;
        using var timer = new PeriodicTimer(Interval, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var leader = await _election.TryHoldAsync(stoppingToken);
                    if (leader != wasLeader)
                    {
                        LogLeadershipChanged(_role, leader);
                        wasLeader = leader;
                    }

                    if (leader)
                    {
                        await using var scope = _scopes.CreateAsyncScope();
                        await SendAsync(scope.ServiceProvider.GetRequiredService<ISender>(), stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogScanFailed(ex, _role);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _election.ResignAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} is disabled on this instance")]
    private partial void LogDisabled(string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} leadership changed: leader={IsLeader}")]
    private partial void LogLeadershipChanged(string role, bool isLeader);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Role} scan failed")]
    private partial void LogScanFailed(Exception ex, string role);
}
