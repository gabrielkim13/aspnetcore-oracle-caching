// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.Caching.Oracle;

public class CacheItemInfo
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required string Id { get; set; }

    public required byte[] Value { get; init; }

    public DateTimeOffset ExpiresAtTime { get; init; }

    public TimeSpan? SlidingExpirationInSeconds { get; set; }

    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
