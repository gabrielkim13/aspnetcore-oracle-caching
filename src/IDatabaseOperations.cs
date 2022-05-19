using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.Caching.Oracle;

internal interface IDatabaseOperations
{
    byte[]? GetCacheItem(string key);

    Task<byte[]?> GetCacheItemAsync(string key, CancellationToken token = default);

    void RefreshCacheItem(string key);

    Task RefreshCacheItemAsync(string key, CancellationToken token = default);

    void DeleteCacheItem(string key);

    Task DeleteCacheItemAsync(string key, CancellationToken token = default);

    void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options);

    Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default);

    void DeleteExpiredCacheItems();
}