using FluentValidation;

namespace LockGuard.Application.Features.Locks.Queries.GetLockEvents;

public sealed class GetLockEventsQueryValidator : AbstractValidator<GetLockEventsQuery>
{
    public GetLockEventsQueryValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, GetLockEventsQuery.MaxCount);
        RuleFor(x => x.Kind).IsInEnum();
    }
}
