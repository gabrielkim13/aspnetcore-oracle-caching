using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Caching.Oracle;

public static class OracleCachingServicesExtensions
{
    public static IServiceCollection AddDistributedOracleCache(this IServiceCollection services,
        Action<OracleCacheOptions> setupAction)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        if (setupAction is null) throw new ArgumentNullException(nameof(setupAction));

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