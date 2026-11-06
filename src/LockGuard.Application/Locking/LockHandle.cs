
namespace LockGuard.Application.Locking;

/// <summary>
/// One acquisition of a resource inside a <see cref="LockSession"/>. Disposing it releases that acquisition; the lock is
/// released in the store when the outermost (first) handle of the resource is disposed.
/// </summary>
public sealed class LockHandle : IAsyncDisposable
{
    private readonly LockSession _session;
    private int _released;

    internal LockHandle(LockSession session, string resource, long fencingToken, bool isReentrant)
    {
        _session = session;
        Resource = resource;
        FencingToken = fencingToken;
        IsReentrant = isReentrant;
    }

    public string Resource { get; }

    public string SessionId => _session.SessionId;

    /// <summary>
    /// Monotonically increasing token issued by the store on each fresh acquisition. Downstream resources should reject
    /// writes carrying a token lower than the last one they saw (protects against GC pauses / clock jumps, see README).
    /// </summary>
    public long FencingToken { get; }

    /// <summary><c>true</c> when the session already held the lock (in-process re-entry or idempotent retry).</summary>
    public bool IsReentrant { get; }

    public bool IsReleased => Volatile.Read(ref _released) == 1;

    public ValueTask DisposeAsync() =>
        Interlocked.Exchange(ref _released, 1) == 0 ? _session.ReleaseOneAsync(Resource) : ValueTask.CompletedTask;
}
