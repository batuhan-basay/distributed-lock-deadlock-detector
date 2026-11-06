using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Locks;
using MediatR;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

public sealed class SimulateCrashedOwnerCommandHandler(ILockStore store, IOptions<LockGuardOptions> options)
    : IRequestHandler<SimulateCrashedOwnerCommand, CrashedOwnerResponse>
{
    public async Task<CrashedOwnerResponse> Handle(SimulateCrashedOwnerCommand request, CancellationToken cancellationToken)
    {
        var deadInstance = $"crashed-{Guid.NewGuid().ToString("N")[..8]}";
        var owner = new LockOwner($"s-orphan-{Guid.NewGuid():N}", options.Value.ServiceName, deadInstance);

        var result = await store.TryAcquireAsync(request.Resource, owner, TimeSpan.FromSeconds(request.TtlSeconds), cancellationToken);
        if (!result.IsSuccess)
        {
            return new CrashedOwnerResponse(ScenarioOutcome.ResourceBusy, request.Resource, request.TtlSeconds,
                HolderSessionId: result.CurrentHolderSessionId);
        }

        var reclaimer = options.Value.Reclaimer;
        return new CrashedOwnerResponse(
            ScenarioOutcome.Accepted,
            request.Resource,
            request.TtlSeconds,
            owner.SessionId,
            deadInstance,
            $"~{(reclaimer.MinLockAge + reclaimer.ScanInterval).TotalSeconds:F0}s (MinLockAge + ScanInterval) instead of {request.TtlSeconds}s");
    }
}
