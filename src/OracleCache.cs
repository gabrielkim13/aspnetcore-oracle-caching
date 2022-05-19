using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Caching.Oracle;

public class OracleCache : IDistributedCache
{
    private static readonly TimeSpan MinimumExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);

    private readonly IDatabaseOperations _dbOperations;
    private readonly TimeSpan _defaultSlidingExpiration;
    private readonly Action _deleteExpiredCachedItemsDelegate;
    private readonly TimeSpan _expiredItemsDeletionInterval;
    private readonly object _mutex = new();
    private readonly ISystemClock _systemClock;
    private DateTimeOffset _lastExpirationScan;

    public OracleCache(IOptions<OracleCacheOptions> options)
    {
        var cacheOptions = options.Value;

        if (string.IsNullOrEmpty(cacheOptions.ConnectionString))
            throw new ArgumentException($"{nameof(OracleCacheOptions.ConnectionString)} cannot be empty or null.");

        if (string.IsNullOrEmpty(cacheOptions.SchemaName))
            throw new ArgumentException($"{nameof(OracleCacheOptions.SchemaName)} cannot be empty or null.");

        if (string.IsNullOrEmpty(cacheOptions.TableName))
            throw new ArgumentException($"{nameof(OracleCacheOptions.TableName)} cannot be empty or null.");

        if (cacheOptions.ExpiredItemsDeletionInterval.HasValue &&
            cacheOptions.ExpiredItemsDeletionInterval.Value < MinimumExpiredItemsDeletionInterval)
            throw new ArgumentException(
                $"{nameof(OracleCacheOptions.ExpiredItemsDeletionInterval)} cannot be less than the minimum value of {MinimumExpiredItemsDeletionInterval.TotalMinutes} minutes.");

        if (cacheOptions.DefaultSlidingExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(cacheOptions.DefaultSlidingExpiration),
                cacheOptions.DefaultSlidingExpiration,
                "The sliding expiration value must be positive."
            );

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _systemClock = cacheOptions.SystemClock ?? new SystemClock();
        _expiredItemsDeletionInterval =
            cacheOptions.ExpiredItemsDeletionInterval ?? DefaultExpiredItemsDeletionInterval;
        _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
        _defaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration;

        _dbOperations = new DatabaseOperations(
            cacheOptions.ConnectionString,
            cacheOptions.SchemaName,
            cacheOptions.TableName,
            _systemClock);
    }

    public byte[]? Get(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        var value = _dbOperations.GetCacheItem(key);

        ScanForExpiredItemsIfRequired();

        return value;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = new())
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        token.ThrowIfCancellationRequested();

        var value = await _dbOperations.GetCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();

        return value;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (options is null) throw new ArgumentNullException(nameof(options));

        GetOptions(ref options);

        _dbOperations.SetCacheItem(key, value, options);

        ScanForExpiredItemsIfRequired();
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = new())
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (options is null) throw new ArgumentNullException(nameof(options));

        token.ThrowIfCancellationRequested();

        GetOptions(ref options);

        await _dbOperations.SetCacheItemAsync(key, value, options, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    public void Refresh(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _dbOperations.RefreshCacheItem(key);

        ScanForExpiredItemsIfRequired();
    }

    public async Task RefreshAsync(string key, CancellationToken token = new())
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        token.ThrowIfCancellationRequested();

        await _dbOperations.RefreshCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    public void Remove(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _dbOperations.DeleteCacheItem(key);

        ScanForExpiredItemsIfRequired();
    }

    public async Task RemoveAsync(string key, CancellationToken token = new())
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        token.ThrowIfCancellationRequested();

        await _dbOperations.DeleteCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    private void ScanForExpiredItemsIfRequired()
    {
        lock (_mutex)
        {
            var utcNow = _systemClock.UtcNow;

            if (utcNow - _lastExpirationScan <= _expiredItemsDeletionInterval) return;

            _lastExpirationScan = utcNow;
            Task.Run(_deleteExpiredCachedItemsDelegate);
        }
    }

    private void DeleteExpiredCacheItems()
    {
        _dbOperations.DeleteExpiredCacheItems();
    }

    private void GetOptions(ref DistributedCacheEntryOptions options)
    {
        if (!options.AbsoluteExpiration.HasValue
            && !options.AbsoluteExpirationRelativeToNow.HasValue
            && !options.SlidingExpiration.HasValue)
            options = new DistributedCacheEntryOptions {SlidingExpiration = _defaultSlidingExpiration};
    }
}