<a id="readme-top"></a>

<br />
<div align="center">
  <h3 align="center">ASP.NET Core - Oracle Database Distributed Cache</h3>
</div>

<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about">About</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites">Prerequisites</a></li>
        <li><a href="#instalation">Instalation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contact">Contact</a></li>
  </ol>
</details>

## About

ASP.NET Core's Distributed Cache implementation using Oracle Database.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Built With

- [![.NET][.NET]][.NET-url]
  - [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Getting Started

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/)
  - [Docker Compose](https://docs.docker.com/compose/install/)
- [Git](https://git-scm.com/downloads)
- [.NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows)

### Instalation

Restore the solution do download its projects' dependencies:

```sh
dotnet restore
```

And use Docker Compose to provision the containerized infrastructure:

```sh
docker compose -p aspnetcore-oracle-caching up -d --build
```

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Usage

To add the Oracle distributed cache service to your application, add these lines to your startup
code:

```c#
builder.Services.AddDistributedOracleCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DistCache_ConnectionString");
    options.SchemaName = "CACHING";
    options.TableName = "Cache";
});
```

For more details, on using distributed cache in ASP.NET Core, refer to the docs: 
[Distributed caching in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed).

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Roadmap

> *Imitation is the sincerest form of flattery*

As it so happens, this package is essentially a port of the SQL Server distributed cache implemented
on ASP.NET Core:
[Microsoft.Extensions.Caching.SqlServer](https://github.com/dotnet/aspnetcore/tree/main/src/Caching)

In order to add it to the ASP.NET Core's main repository, we would need to have:

- [X] All tests passing
- [ ] Properly documented code
- [ ] Means of publishing this package to NuGet under its intended name, i.e. `Microsoft.Extensions.Caching.Oracle`

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contact

- This package is a modified version of [aspnetcore-oracle-caching] (https://github.com/gabrielkim13/aspnetcore-oracle-caching),
originally developed by [Gabriel Kim] under the MIT License.


<p align="right">(<a href="#readme-top">back to top</a>)</p>

[.NET]: https://img.shields.io/badge/.NET-5C2D91?style=badge&logo=.net&logoColor=white
[.NET-url]: https://dotnet.microsoft.com/en-us/
