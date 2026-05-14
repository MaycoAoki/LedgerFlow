using StackExchange.Redis;
using LedgerFlow.Domain.Interfaces;
using System.Text.Json;

namespace LedgerFlow.Infrastructure.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }
}
