namespace Vendor.Application.Common.Interfaces;

public interface IIdempotencyLockManager
{
    Task<IDisposable?> AcquireLockAsync(Guid keyUuid, TimeSpan timeout, CancellationToken ct = default);
}
