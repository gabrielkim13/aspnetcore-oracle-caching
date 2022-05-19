using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Caching.Oracle;

internal class TestOracleCacheOptions : IOptions<OracleCacheOptions>
{
    public TestOracleCacheOptions(OracleCacheOptions innerOptions)
    {
        Value = innerOptions;
    }

    public OracleCacheOptions Value { get; }
}