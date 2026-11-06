using LockGuard.Application;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Maintenance.Commands.ScanDeadlocks;
using LockGuard.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LockGuard.UnitTests.TestDoubles;

/// <summary>
/// The real Application layer (MediatR handlers + validation pipeline) on top of the in-memory persistence adapter.
/// Hosted background services are not started; tests drive the detector through <see cref="ScanDeadlocksCommand"/>.
/// </summary>
internal sealed class MediatorTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public MediatorTestHost(Action<LockGuardOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddApplication(o =>
            {
                o.ServiceName = "unit-tests";
                o.InstanceId = "test-instance";
                o.Acquisition.InitialRetryDelay = TimeSpan.FromMilliseconds(2);
                o.Acquisition.MaxRetryDelay = TimeSpan.FromMilliseconds(20);
                o.Acquisition.WaitTimeout = TimeSpan.FromSeconds(10);
                o.Detection.ConfirmationScans = 2;
                configure?.Invoke(o);
            })
            .AddInMemoryPersistence();

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public IServiceProvider Services => _provider;

    /// <summary>Sends a request in its own scope, like an HTTP request.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
    }

    /// <summary>What the leader-elected <c>DeadlockDetectorService</c> does in production.</summary>
    public async Task RunDetectorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await SendAsync(new ScanDeadlocksCommand(), cancellationToken);
                await Task.Delay(50, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
