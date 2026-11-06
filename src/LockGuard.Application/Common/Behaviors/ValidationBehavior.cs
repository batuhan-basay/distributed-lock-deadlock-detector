using FluentValidation;
using MediatR;

namespace LockGuard.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline step that runs every FluentValidation validator registered for the request and throws a
/// <see cref="ValidationException"/> (mapped to HTTP 400 by the API) before the handler is invoked.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var all = validators.ToList();
        if (all.Count == 0)
        {
            return await next(cancellationToken);
        }

        var results = await Task.WhenAll(all.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
