using FluentValidation;
using LockGuard.Application.Common.Validation;

namespace LockGuard.Application.Features.Locks.Queries.GetLocks;

public sealed class GetLocksQueryValidator : AbstractValidator<GetLocksQuery>
{
    public GetLocksQueryValidator()
    {
        RuleFor(x => x.ResourcePrefix).OptionalMaxLength(RuleBuilderExtensions.MaxResourceLength);
    }
}
