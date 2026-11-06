using System.Text.RegularExpressions;
using FluentValidation;
using LockGuard.Domain.Coordination;

namespace LockGuard.Application.Common.Validation;

public static partial class RuleBuilderExtensions
{
    public const int MaxResourceLength = 200;
    public const int MaxSessionIdLength = 128;

    public static IRuleBuilderOptions<T, string> ValidResource<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MaximumLength(MaxResourceLength)
            .Must(resource => resource is null || !InternalResources.IsInternal(resource))
            .WithMessage($"Resources starting with '{InternalResources.Prefix}' are reserved for LockGuard.");

    public static IRuleBuilderOptions<T, string?> ValidOptionalSessionId<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(id => id is null || (id.Length is > 0 and <= MaxSessionIdLength && SessionIdPattern().IsMatch(id)))
            .WithMessage($"Session id must be 1-{MaxSessionIdLength} characters of letters, digits, ':', '_', '.' or '-'.");

    public static IRuleBuilderOptions<T, string?> OptionalMaxLength<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule
            .Must(value => value is null || value.Length <= maxLength)
            .WithMessage($"Must be at most {maxLength} characters.");

    [GeneratedRegex("^[A-Za-z0-9:_.-]+$")]
    private static partial Regex SessionIdPattern();
}
