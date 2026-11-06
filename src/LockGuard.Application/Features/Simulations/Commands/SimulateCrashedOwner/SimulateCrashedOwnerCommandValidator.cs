using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

public sealed class SimulateCrashedOwnerCommandValidator : AbstractValidator<SimulateCrashedOwnerCommand>
{
    public SimulateCrashedOwnerCommandValidator()
    {
        RuleFor(x => x.Resource).ValidResource();
        RuleFor(x => x.TtlSeconds).InclusiveBetween(1, 86_400);
    }
}
