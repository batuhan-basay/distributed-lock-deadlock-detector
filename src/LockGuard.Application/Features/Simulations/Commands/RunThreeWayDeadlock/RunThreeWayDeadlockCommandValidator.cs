using FluentValidation;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed class RunThreeWayDeadlockCommandValidator : AbstractValidator<RunThreeWayDeadlockCommand>
{
    public RunThreeWayDeadlockCommandValidator()
    {
        
        RuleFor(x => x.HoldMs)
            .InclusiveBetween(100, 10_000)
            .WithMessage("HoldMs must be between 100 and 10000 ms so that all three sessions hold their first lock at the same time.");
    }
}
