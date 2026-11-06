using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LockGuard.Application.Common.Behaviors;

/// <summary>MediatR pipeline step that logs every request with its duration.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(cancellationToken);
            logger.LogDebug("Handled {Request} in {ElapsedMs:F1} ms", name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "{Request} failed after {ElapsedMs:F1} ms", name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
