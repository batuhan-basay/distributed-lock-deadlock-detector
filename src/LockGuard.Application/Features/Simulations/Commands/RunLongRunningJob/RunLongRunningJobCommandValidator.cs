using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;

public sealed class RunLongRunningJobCommandValidator : AbstractValidator<RunLongRunningJobCommand>
{
    public RunLongRunningJobCommandValidator()
    {
        RuleFor(x => x.Resource).ValidResource();
        RuleFor(x => x.WorkMs).InclusiveBetween(0, 300_000);
        RuleFor(x => x.LeaseMs)
            .InclusiveBetween(500, 60_000)
            .WithMessage("LeaseMs must be between 500 and 60000 ms (the watchdog renews every LeaseMs / 3).");
    }
}
