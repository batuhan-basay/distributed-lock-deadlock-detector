namespace LockGuard.Api.Http;

public static class LockGuardHeaders
{
    /// <summary>Optional idempotency key: a retried request with the same value re-enters the locks it already holds.</summary>
    public const string SessionId = "X-Lock-Session-Id";
}
