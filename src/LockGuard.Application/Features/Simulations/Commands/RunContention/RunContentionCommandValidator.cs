using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.RunContention;

public sealed class RunContentionCommandValidator : AbstractValidator<RunContentionCommand>
{
    public RunContentionCommandValidator()
    {
        RuleFor(x => x.Resource).ValidResource();
        RuleFor(x => x.WorkMs).InclusiveBetween(0, 10_000);
        RuleFor(x => x.WaitTimeoutMs).InclusiveBetween(10, 60_000);
        RuleFor(x => x.SessionId).ValidOptionalSessionId();
    }
}
