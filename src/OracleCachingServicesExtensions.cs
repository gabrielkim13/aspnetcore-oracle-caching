using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Caching.Oracle;

public static class OracleCachingServicesExtensions
{
    public static IServiceCollection AddDistributedOracleCache(this IServiceCollection services,
        Action<OracleCacheOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddOptions();
        AddOracleCacheServices(services);
        services.Configure(setupAction);

        return services;
    }

    public static void AddOracleCacheServices(IServiceCollection services)
    {
        services.Add(ServiceDescriptor.Singleton<IDistributedCache, OracleCache>());
    }
}
