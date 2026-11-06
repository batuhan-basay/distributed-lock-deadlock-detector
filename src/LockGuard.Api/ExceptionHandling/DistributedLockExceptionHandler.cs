using LockGuard.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LockGuard.Api.ExceptionHandling;

/// <summary>
/// Maps lock exceptions that escape a handler to problem details: deadlock victim / lost lock → 409, wait timeout → 423.
/// (The simulation handlers translate the expected ones into business outcomes themselves.)
/// </summary>
public sealed class DistributedLockExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DistributedLockException lockException)
        {
            return false;
        }

        var (status, title) = lockException switch
        {
            DeadlockVictimException => (StatusCodes.Status409Conflict, "Aborted as deadlock victim"),
            LockLostException => (StatusCodes.Status409Conflict, "Lock lost"),
            LockAcquisitionTimeoutException => (StatusCodes.Status423Locked, "Lock acquisition timed out"),
            _ => (StatusCodes.Status409Conflict, "Distributed lock error"),
        };

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = lockException.Message,
            Extensions =
            {
                ["resource"] = lockException.Resource,
                ["sessionId"] = lockException.SessionId,
                ["retryable"] = lockException is DeadlockVictimException,
            },
        };

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = details,
        });
    }
}
