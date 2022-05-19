// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.Caching.Oracle;

public class CacheItemInfo
{
    public string Id { get; set; }

    public byte[] Value { get; set; }

    public DateTimeOffset ExpiresAtTime { get; set; }

    public TimeSpan? SlidingExpirationInSeconds { get; set; }

    public DateTimeOffset? AbsoluteExpiration { get; set; }
}