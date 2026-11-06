using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;

public sealed class RunOrderedTransferCommandValidator : AbstractValidator<RunOrderedTransferCommand>
{
    public RunOrderedTransferCommandValidator()
    {
        RuleFor(x => x.FirstResource).ValidResource();
        RuleFor(x => x.SecondResource)
            .ValidResource()
            .NotEqual(x => x.FirstResource)
            .WithMessage("The second resource must differ from the first one.");
        RuleFor(x => x.WorkMs).InclusiveBetween(0, 30_000);
        RuleFor(x => x.SessionId).ValidOptionalSessionId();
    }
}
