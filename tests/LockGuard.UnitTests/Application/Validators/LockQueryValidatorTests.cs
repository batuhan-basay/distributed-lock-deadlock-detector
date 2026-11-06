using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Application.Features.Locks.Queries.GetLocks;
using LockGuard.Domain.Events;

namespace LockGuard.UnitTests.Application.Validators;

public class LockQueryValidatorTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(0, false)]
    [InlineData(201, false)]
    public void Event_count_must_be_within_bounds(int count, bool valid) =>
        Assert.Equal(valid, new GetLockEventsQueryValidator().Validate(new GetLockEventsQuery(count)).IsValid);

    [Fact]
    public void Undefined_event_kind_is_rejected()
    {
        var result = new GetLockEventsQueryValidator().Validate(new GetLockEventsQuery(10, (LockEventKind)42));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetLockEventsQuery.Kind));
    }

    [Fact]
    public void Resource_prefix_is_optional_but_bounded()
    {
        var validator = new GetLocksQueryValidator();

        Assert.True(validator.Validate(new GetLocksQuery()).IsValid);
        Assert.True(validator.Validate(new GetLocksQuery("account:")).IsValid);
        Assert.False(validator.Validate(new GetLocksQuery(new string('x', 201))).IsValid);
    }
}
