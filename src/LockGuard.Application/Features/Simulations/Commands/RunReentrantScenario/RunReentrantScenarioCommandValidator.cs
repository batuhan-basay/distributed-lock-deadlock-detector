using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

public sealed class RunReentrantScenarioCommandValidator : AbstractValidator<RunReentrantScenarioCommand>
{
    public RunReentrantScenarioCommandValidator()
    {
        RuleFor(x => x.Resource).ValidResource();
        RuleFor(x => x.Depth).InclusiveBetween(1, 10);
        RuleFor(x => x.HoldMs).InclusiveBetween(0, 30_000);
        RuleFor(x => x.SessionId).ValidOptionalSessionId();
    }
}
