using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;

public sealed class RunDeadlockScenarioCommandValidator : AbstractValidator<RunDeadlockScenarioCommand>
{
    public RunDeadlockScenarioCommandValidator()
    {
        RuleFor(x => x.Scenario).NotEmpty().MaximumLength(64);
        RuleFor(x => x.FirstResource).ValidResource();
        RuleFor(x => x.SecondResource)
            .ValidResource()
            .NotEqual(x => x.FirstResource)
            .WithMessage("The second resource must differ from the first one, otherwise there is nothing to deadlock on.");
        RuleFor(x => x.HoldMs).InclusiveBetween(0, 30_000);
        RuleFor(x => x.WorkMs).InclusiveBetween(0, 30_000);
        RuleFor(x => x.MaxRetries).InclusiveBetween(0, 5);
        RuleFor(x => x.SessionId).ValidOptionalSessionId();
    }
}
