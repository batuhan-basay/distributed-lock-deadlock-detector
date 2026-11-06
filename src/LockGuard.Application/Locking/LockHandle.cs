
namespace LockGuard.Application.Locking;

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

    public long FencingToken { get; }

    public bool IsReentrant { get; }

    public bool IsReleased => Volatile.Read(ref _released) == 1;

    public ValueTask DisposeAsync() =>
        Interlocked.Exchange(ref _released, 1) == 0 ? _session.ReleaseOneAsync(Resource) : ValueTask.CompletedTask;
}
