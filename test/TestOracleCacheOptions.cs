using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Caching.Oracle;

// ReSharper disable once UnusedType.Global
internal class TestOracleCacheOptions(OracleCacheOptions innerOptions) : IOptions<OracleCacheOptions>
{
    public OracleCacheOptions Value { get; } = innerOptions;
}
