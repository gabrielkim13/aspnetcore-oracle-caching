using System.Data;
using System.Globalization;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;

using Oracle.ManagedDataAccess.Client;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Caching.Oracle;

public class OracleCacheWithDatabaseTest
{
    private const string ConnectionStringKey = "ConnectionString";
    private const string SchemaNameKey = "SchemaName";
    private const string TableNameKey = "TableName";
    private readonly string _connectionString;
    private readonly string _schemaName;
    private readonly string _tableName;

    public OracleCacheWithDatabaseTest()
    {
        var memoryConfigurationData = new Dictionary<string, string>
        {
            // When creating a test database, these values must be used in the parameters to 'dotnet sql-cache create'.
            // If you have to use other parameters for some reason, make sure to update this!
            {
                ConnectionStringKey,
                "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1522))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=IASLineControl;Password=dbo;"
            },
            { SchemaNameKey, "IASLineControl" },
            { TableNameKey, "SNAPSHOTCACHE" }
        };

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(memoryConfigurationData!);

        var configuration = configurationBuilder.Build();
        _tableName = configuration[TableNameKey]!;
        _schemaName = configuration[SchemaNameKey]!;
        _connectionString = configuration[ConnectionStringKey]!;
    }

    [Fact]
    public async Task ReturnsNullValue_ForNonExistingCacheItem()
    {
        // Arrange
        var cache = GetOracleCache();

        // Act
        var value = await cache.GetAsync("NonExisting");

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task SetWithAbsoluteExpirationSetInThePast_Throws()
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var expectedValue = "Hello, World!"u8.ToArray();
        var cache = GetOracleCache(GetCacheOptions(testClock));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(testClock.UtcNow.AddHours(-1))));
        Assert.Equal("The absolute expiration value must be in the future.", exception.Message);
    }

    [Fact]
    public async Task SetCacheItem_SucceedsFor_KeyEqualToMaximumSize()
    {
        // Arrange
        // Create a key with the maximum allowed key length. Here a key of length 898 bytes is created.
        var key = new string('a', OracleParameterCollectionExtensions.CacheItemIdColumnWidth);
        var testClock = new TestClock();
        var expectedValue = "Hello, World!"u8.ToArray();
        var cache = GetOracleCache(GetCacheOptions(testClock));

        // Act
        await cache.SetAsync(
            key, expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

        // Assert
        var cacheItem = await GetCacheItemFromDatabaseAsync(key);
        Assert.Equal(expectedValue, cacheItem!.Value);

        // Act
        await cache.RemoveAsync(key);

        // Assert
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.Null(cacheItemInfo);
    }

    [Fact]
    public async Task SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var testClock = new TestClock();
        var expectedValue = "Hello, World!"u8.ToArray();
        var cacheOptions = GetCacheOptions(testClock);
        var cache = GetOracleCache(cacheOptions);
        var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);

        // Act
        await cache.SetAsync(key, expectedValue,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = null, AbsoluteExpirationRelativeToNow = null, SlidingExpiration = null
            });

        // Assert
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            cacheOptions.DefaultSlidingExpiration,
            null,
            expectedExpirationTime);

        var cacheItem = await GetCacheItemFromDatabaseAsync(key);
        Assert.Equal(expectedValue, cacheItem!.Value);

        // Act
        await cache.RemoveAsync(key);

        // Assert
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.Null(cacheItemInfo);
    }

    [Fact]
    public async Task UpdatedDefaultSlidingExpiration_SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var testClock = new TestClock();
        var expectedValue = "Hello, World!"u8.ToArray();
        var cacheOptions = GetCacheOptions(testClock);
        cacheOptions.DefaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration.Add(TimeSpan.FromMinutes(10));
        var cache = GetOracleCache(cacheOptions);
        var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);

        // Act
        await cache.SetAsync(key, expectedValue,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = null, AbsoluteExpirationRelativeToNow = null, SlidingExpiration = null
            });

        // Assert
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            cacheOptions.DefaultSlidingExpiration,
            null,
            expectedExpirationTime);

        var cacheItem = await GetCacheItemFromDatabaseAsync(key);
        Assert.Equal(expectedValue, cacheItem!.Value);

        // Act
        await cache.RemoveAsync(key);

        // Assert
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.Null(cacheItemInfo);
    }

    [Fact]
    public async Task SetCacheItem_FailsFor_KeyGreaterThanMaximumSize()
    {
        // Arrange
        // Create a key which is greater than the maximum length.
        var key = new string('b', OracleParameterCollectionExtensions.CacheItemIdColumnWidth + 1);
        var testClock = new TestClock();
        var expectedValue = "Hello, World!"u8.ToArray();
        var cache = GetOracleCache(GetCacheOptions(testClock));

        // Act
        await cache.SetAsync(
            key, expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

        // Assert
        var cacheItem = await GetCacheItemFromDatabaseAsync(key);
        Assert.Null(cacheItem);
    }

    // Arrange
    [Theory]
    [InlineData(10, 11)]
    [InlineData(10, 30)]
    public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem(
        int slidingExpirationWindow, int accessItemAt)
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        await cache.SetAsync(
            key,
            "Hello, World!"u8.ToArray(),
            new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(slidingExpirationWindow)));

        // set the clock's UtcNow far in future
        testClock.Add(TimeSpan.FromHours(accessItemAt));

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.Null(value);
    }

    [Theory]
    [InlineData(5, 15)]
    [InlineData(10, 20)]
    public async Task SetWithSlidingExpiration_ExtendsExpirationTime(int accessItemAt, int expected)
    {
        // Arrange
        var testClock = new TestClock();
        var slidingExpirationWindow = TimeSpan.FromSeconds(10);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        var expectedExpirationTime = testClock.UtcNow.AddSeconds(expected);
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

        testClock.Add(TimeSpan.FromSeconds(accessItemAt));
        // Act
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            slidingExpirationWindow,
            null,
            expectedExpirationTime);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(50)]
    public async Task SetWithSlidingExpirationAndAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem(
        int accessItemAt)
    {
        // Arrange
        var testClock = new TestClock();
        var utcNow = testClock.UtcNow;
        var slidingExpiration = TimeSpan.FromSeconds(5);
        var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        await cache.SetAsync(
            key,
            expectedValue,
            // Set both sliding and absolute expiration
            new DistributedCacheEntryOptions()
                .SetSlidingExpiration(slidingExpiration)
                .SetAbsoluteExpiration(absoluteExpiration));

        // Act
        _ = testClock.Add(TimeSpan.FromSeconds(accessItemAt)).UtcNow;
        var value = await cache.GetAsync(key);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task SetWithAbsoluteExpirationRelativeToNow_ReturnsNullValue_ForExpiredCacheItem()
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        await cache.SetAsync(
            key,
            "Hello, World!"u8.ToArray(),
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(10)));

        // set the clock's UtcNow far in future
        testClock.Add(TimeSpan.FromHours(10));

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task SetWithAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem()
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        await cache.SetAsync(
            key,
            "Hello, World!"u8.ToArray(),
            new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(testClock.UtcNow.Add(TimeSpan.FromSeconds(30))));

        // set the clock's UtcNow far in future
        testClock.Add(TimeSpan.FromHours(10));

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpirationRelativeToNow()
    {
        // Arrange
        var testClock = new TestClock();
        var absoluteExpirationRelativeToUtcNow = TimeSpan.FromSeconds(10);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        var expectedAbsoluteExpiration = testClock.UtcNow.Add(absoluteExpirationRelativeToUtcNow);

        // Act
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(absoluteExpirationRelativeToUtcNow));

        // Assert
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            null,
            expectedAbsoluteExpiration,
            expectedAbsoluteExpiration);
    }

    [Fact]
    public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpiration()
    {
        // Arrange
        _ = new TestClock();
        var expectedAbsoluteExpiration = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache();
        var expectedValue = "Hello, World!"u8.ToArray();

        // Act
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(expectedAbsoluteExpiration));

        // Assert
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            null,
            expectedAbsoluteExpiration,
            expectedAbsoluteExpiration);
    }

    [Fact]
    public async Task SetCacheItem_UpdatesAbsoluteExpirationTime()
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

        // Act & Assert
        // Creates a new item
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            null,
            absoluteExpiration,
            absoluteExpiration);

        // Updates an existing item with new absolute expiration time
        absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            null,
            absoluteExpiration,
            absoluteExpiration);
    }

    [Fact]
    public async Task SetCacheItem_WithValueLargerThan_DefaultColumnWidth()
    {
        // Arrange
        var testClock = new TestClock();
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = new byte[OracleParameterCollectionExtensions.DefaultValueColumnWidth + 100];
        var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

        // Act
        // Creates a new item
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));

        // Assert
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            null,
            absoluteExpiration,
            absoluteExpiration);
    }

    [Fact]
    public async Task ExtendsExpirationTime_ForSlidingExpiration()
    {
        // Arrange
        var testClock = new TestClock();
        var slidingExpiration = TimeSpan.FromSeconds(10);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        // The operations Set and Refresh here extend the sliding expiration 2 times.
        var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

        // Act
        testClock.Add(TimeSpan.FromSeconds(5));
        await cache.RefreshAsync(key);

        // Assert
        // verify if the expiration time in database is set as expected
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.NotNull(cacheItemInfo);
        Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
        Assert.Null(cacheItemInfo.AbsoluteExpiration);
        Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
    }

    [Fact]
    public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
    {
        // Arrange
        var testClock = new TestClock();
        var utcNow = testClock.UtcNow;
        var slidingExpiration = TimeSpan.FromSeconds(5);
        var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        await cache.SetAsync(
            key,
            expectedValue,
            // Set both sliding and absolute expiration
            new DistributedCacheEntryOptions()
                .SetSlidingExpiration(slidingExpiration)
                .SetAbsoluteExpiration(absoluteExpiration));

        // Act && Assert
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.NotNull(cacheItemInfo);
        Assert.Equal(utcNow.AddSeconds(5), cacheItemInfo.ExpiresAtTime);

        // Accessing item at time...
        utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            slidingExpiration,
            absoluteExpiration,
            utcNow.AddSeconds(5));

        // Accessing item at time...
        utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            slidingExpiration,
            absoluteExpiration,
            utcNow.AddSeconds(5));

        // Accessing item at time...
        _ = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
        // The expiration extension must not exceed the absolute expiration
        await AssertGetCacheItemFromDatabaseAsync(
            cache,
            key,
            expectedValue,
            slidingExpiration,
            absoluteExpiration,
            absoluteExpiration);
    }

    [Fact]
    public async Task DoestNotExtendsExpirationTime_ForAbsoluteExpiration()
    {
        // Arrange
        var testClock = new TestClock();
        var absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
        var expectedExpiresAtTime = testClock.UtcNow.Add(absoluteExpirationRelativeToNow);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpirationRelativeToNow));
        testClock.Add(TimeSpan.FromSeconds(25));

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(value);
        Assert.Equal(expectedValue, value);

        // verify if the expiration time in database is set as expected
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.NotNull(cacheItemInfo);
        Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
    }

    [Fact]
    public async Task RefreshItem_ExtendsExpirationTime_ForSlidingExpiration()
    {
        // Arrange
        var testClock = new TestClock();
        var slidingExpiration = TimeSpan.FromSeconds(10);
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache(GetCacheOptions(testClock));
        var expectedValue = "Hello, World!"u8.ToArray();
        // The operations Set and Refresh here extend the sliding expiration 2 times.
        var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

        // Act
        testClock.Add(TimeSpan.FromSeconds(5));
        await cache.RefreshAsync(key);

        // Assert
        // verify if the expiration time in database is set as expected
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.NotNull(cacheItemInfo);
        Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
        Assert.Null(cacheItemInfo.AbsoluteExpiration);
        Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
    }

    [Fact]
    public async Task GetCacheItem_IsCaseSensitive()
    {
        // Arrange
        var key = Guid.NewGuid().ToString().ToLower(CultureInfo.InvariantCulture); // lower case
        var cache = GetOracleCache();
        await cache.SetAsync(
            key,
            "Hello, World!"u8.ToArray(),
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

        // Act
        var value = await cache.GetAsync(key.ToUpper(CultureInfo.InvariantCulture)); // key made upper case

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task GetCacheItem_DoesNotTrimTrailingSpaces()
    {
        // Arrange
        var key = string.Format(CultureInfo.InvariantCulture, "  {0}  ", Guid.NewGuid()); // with trailing spaces
        var cache = GetOracleCache();
        var expectedValue = "Hello, World!"u8.ToArray();
        await cache.SetAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(value);
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public async Task DeletesCacheItem_OnExplicitlyCalled()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var cache = GetOracleCache();
        await cache.SetAsync(
            key,
            "Hello, World!"u8.ToArray(),
            new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

        // Act
        await cache.RemoveAsync(key);

        // Assert
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.Null(cacheItemInfo);
    }

    private OracleCache GetOracleCache(OracleCacheOptions? options = null)
    {
        options ??= GetCacheOptions();

        return new OracleCache(options);
    }

    private OracleCacheOptions GetCacheOptions(ISystemClock? testClock = null)
    {
        return new OracleCacheOptions
        {
            ConnectionString = _connectionString,
            SchemaName = _schemaName,
            TableName = _tableName,
            SystemClock = testClock ?? new TestClock(),
            ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
        };
    }

    private async Task AssertGetCacheItemFromDatabaseAsync(
        OracleCache cache,
        string key,
        byte[] expectedValue,
        TimeSpan? slidingExpiration,
        DateTimeOffset? absoluteExpiration,
        DateTimeOffset expectedExpirationTime)
    {
        var value = await cache.GetAsync(key);
        Assert.NotNull(value);
        Assert.Equal(expectedValue, value);
        var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
        Assert.NotNull(cacheItemInfo);
        Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
        Assert.Equal(absoluteExpiration, cacheItemInfo.AbsoluteExpiration);
        Assert.Equal(expectedExpirationTime, cacheItemInfo.ExpiresAtTime);
    }

    private async Task<CacheItemInfo?> GetCacheItemFromDatabaseAsync(string key)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText =
            $"""
             SELECT "ID",
                    "VALUE",
                    TO_CHAR("EXPIRESATTIME", 'YYYY-MM-DD HH24:MI:SSxFF TZH:TZM'),
                    "SLIDINGEXPIRATIONINSECONDS",
                    TO_CHAR("ABSOLUTEEXPIRATION", 'YYYY-MM-DD HH24:MI:SSxFF TZH:TZM')
             FROM "{_tableName}"
             WHERE "ID" = :Id
             """;
        command.Parameters.AddWithValue("Id", OracleDbType.NVarchar2, key);

        var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);

        if (!await reader.ReadAsync()) return null;

        var value = await reader.GetFieldValueAsync<byte[]>(1);
        var expiresAtTime = await reader.GetFieldValueAsync<string>(2);

        var cacheItemInfo = new CacheItemInfo
        {
            Id = key,
            Value = value,
            ExpiresAtTime = DateTimeOffset.Parse(expiresAtTime, CultureInfo.InvariantCulture)
        };

        if (!await reader.IsDBNullAsync(3))
        {
            var slidingExpirationInSeconds = await reader.GetFieldValueAsync<decimal>(3);

            cacheItemInfo.SlidingExpirationInSeconds = TimeSpan.FromSeconds((long)slidingExpirationInSeconds);
        }

        // ReSharper disable once InvertIf
        if (!await reader.IsDBNullAsync(4))
        {
            var absoluteExpiration = await reader.GetFieldValueAsync<string>(4);

            cacheItemInfo.AbsoluteExpiration = DateTimeOffset.Parse(absoluteExpiration, CultureInfo.InvariantCulture);
        }

        return cacheItemInfo;
    }
}
