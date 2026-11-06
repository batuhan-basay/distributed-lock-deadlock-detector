namespace LockGuard.Domain.Locks;

/// <summary>Result of one acquisition attempt.</summary>
public sealed record AcquireResult(
    AcquireStatus Status,
    long FencingToken,
    string? CurrentHolderSessionId,
    TimeSpan? Validity)
{
    public bool IsSuccess => Status is AcquireStatus.Acquired or AcquireStatus.AlreadyOwned;

    public static AcquireResult Contended(string? holder, TimeSpan? holderRemainingTtl) =>
        new(AcquireStatus.Contended, 0, holder, holderRemainingTtl);
}
