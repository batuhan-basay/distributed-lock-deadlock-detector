using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Api.Http;

public static class ScenarioResultExtensions
{
    public static IResult ToHttpResult<TResponse>(this TResponse response)
        where TResponse : IScenarioResponse => response.Outcome switch
    {
        ScenarioOutcome.Completed => Results.Ok(response),
        ScenarioOutcome.Accepted => Results.Accepted("/api/locks/events", response),
        ScenarioOutcome.Timeout => Results.Json(response, statusCode: StatusCodes.Status423Locked),
        ScenarioOutcome.DeadlockVictim or ScenarioOutcome.LockLost or ScenarioOutcome.ResourceBusy =>
            Results.Json(response, statusCode: StatusCodes.Status409Conflict),
        _ => Results.Ok(response),
    };
}
