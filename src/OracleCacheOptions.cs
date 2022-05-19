using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Caching.Oracle;

public class OracleCacheOptions : IOptions<OracleCacheOptions>
{
    public ISystemClock SystemClock { get; set; } = new SystemClock();

    public TimeSpan? ExpiredItemsDeletionInterval { get; set; }

    public string? ConnectionString { get; set; }

    public string? SchemaName { get; set; }

    public string? TableName { get; set; }

    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

    OracleCacheOptions IOptions<OracleCacheOptions>.Value => this;
}