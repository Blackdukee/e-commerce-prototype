using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vendor.Application.Interfaces;

namespace Vendor.Infrastructure.Persistence;

public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class DbIdempotencyStore(VendorDbContext dbContext) : IIdempotencyStore
{
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await dbContext.Set<IdempotencyRecord>().AnyAsync(r => r.Key == key, ct);
    }

    public async Task<TResponse?> GetResultAsync<TResponse>(string key, CancellationToken ct = default)
    {
        var record = await dbContext.Set<IdempotencyRecord>().FirstOrDefaultAsync(r => r.Key == key, ct);
        if (record == null) return default;

        return JsonSerializer.Deserialize<TResponse>(record.ResultJson);
    }

    public async Task SaveResultAsync<TResponse>(string key, TResponse result, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(result);
        var record = new IdempotencyRecord
        {
            Key = key,
            ResultJson = json,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Set<IdempotencyRecord>().Add(record);
        await dbContext.SaveChangesAsync(ct);
    }
}
