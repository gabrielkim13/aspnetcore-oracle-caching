# Caching - Oracle Database

ASP.NET Core's Distributed Cache implementation using Oracle Database.

## Setup

### Requirements

- [Git](https://git-scm.com/downloads)
- [Docker](https://docs.docker.com/get-docker/)
    - [Docker Compose](https://docs.docker.com/compose/install/)
- [Visual Studio](https://visualstudio.microsoft.com/downloads/) ou [Rider](https://www.jetbrains.com/rider/)
- [.NET](https://dotnet.microsoft.com/en-us/download/dotnet)
    - 6.0 (LTS)

### Oracle Database

To use and test this package, it is necessary to have the Docker image `oracle/database:19.3.0-ee` installed.

Unfortunately, Oracle doesn't provide it on [Docker Hub](https://hub.docker.com/), but it can be built from scratch by
following the instructions on https://github.com/oracle/docker-images/tree/main/OracleDatabase/SingleInstance.

Alternatively, it is also possible to pull it from the author's own Docker Hub account:

```bash
docker pull gabrielkim13/oracle-database:19.3.0-ee
docker tag gabrielkim13/oracle-database:19.3.0-ee oracle/database:19.3.0-ee
```

Finally, deploy the Oracle Database container via the `docker-compose.yaml`, with the following command:

```bash
docker-compose -p caching up -d
```

## Instructions

To add the Oracle distributed cache service to your application, add these lines to your startup code:

```c#
builder.Services.AddDistributedOracleCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DistCache_ConnectionString");
    options.SchemaName = "Caching";
    options.TableName = "Cache";
});
```

For more details, on using distributed cache in ASP.NET Core, refer to the docs: 
[Distributed caching in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-6.0).

## Credits

> *Imitation is the sincerest form of flattery*

As it so happens, this package is essentially a port of the SQL Server distributed cache implemented on ASP.NET Core:
[Microsoft.Extensions.Caching.SqlServer](https://github.com/dotnet/aspnetcore/tree/main/src/Caching)

In order to add it to the ASP.NET Core's main repository, we would need to have:

- [X] All tests passing
- [ ] Properly documented code
- [ ] Means of publishing this package to NuGet under its intended name, i.e. `Microsoft.Extensions.Caching.Oracle`

## Authors

- **Gabriel Kim** - *Developer / Maintainer* - [gabrielkim13@gmail.com]()