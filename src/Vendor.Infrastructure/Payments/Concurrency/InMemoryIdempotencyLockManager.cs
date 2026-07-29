using System.Collections.Concurrent;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Concurrency;

public class InMemoryIdempotencyLockManager : IIdempotencyLockManager
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable?> AcquireLockAsync(Guid keyUuid, TimeSpan timeout, CancellationToken ct = default)
    {
        var semaphore = _locks.GetOrAdd(keyUuid, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(timeout, ct);

        if (!acquired)
        {
            return null;
        }

        return new LockReleaser(() =>
        {
            semaphore.Release();
            if (semaphore.CurrentCount == 1)
            {
                _locks.TryRemove(keyUuid, out _);
            }
        });
    }

    private sealed class LockReleaser(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
