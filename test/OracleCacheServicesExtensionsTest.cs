using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Caching.Oracle;

public class OracleCacheServicesExtensionsTest
{
    [Fact]
    public void AddDistributedOracleCache_AddsAsSingleRegistrationService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        OracleCachingServicesExtensions.AddOracleCacheServices(services);

        // Assert
        var serviceDescriptor = Assert.Single(services);
        Assert.Equal(typeof(IDistributedCache), serviceDescriptor.ServiceType);
        Assert.Equal(typeof(OracleCache), serviceDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void AddDistributedOracleCache_ReplacesPreviouslyUserRegisteredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(typeof(IDistributedCache), _ => Mock.Of<IDistributedCache>());

        // Act
        services.AddDistributedOracleCache(options =>
        {
            options.ConnectionString = "Fake";
            options.SchemaName = "Fake";
            options.TableName = "Fake";
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));

        Assert.NotNull(distributedCache);
        Assert.Equal(ServiceLifetime.Scoped, distributedCache!.Lifetime);
        Assert.IsType<OracleCache>(serviceProvider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public void AddDistributedOracleCache_allows_chaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddDistributedOracleCache(_ => { }));
    }
}