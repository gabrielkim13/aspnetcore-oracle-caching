using System.Data;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.Caching.Oracle;

public class DatabaseOperations : IDatabaseOperations
{
    private const int DuplicateKeyErrorId = 1; // ORA-00001

    public DatabaseOperations(string connectionString, string schemaName, string tableName, ISystemClock systemClock)
    {
        ConnectionString = connectionString;
        SchemaName = schemaName;
        TableName = tableName;
        SystemClock = systemClock;
        SqlQueries = new SqlQueries(schemaName, tableName);
    }

    internal SqlQueries SqlQueries { get; }

    internal string ConnectionString { get; }

    internal string SchemaName { get; }

    internal string TableName { get; }

    private ISystemClock SystemClock { get; }

    public byte[]? GetCacheItem(string key)
    {
        // when retrieving an item, we do an UPDATE first and then a SELECT
        InternalRefreshCacheItem(key);

        return InternalGetCacheItem(key);
    }

    public async Task<byte[]?> GetCacheItemAsync(string key, CancellationToken token = default)
    {
        // when retrieving an item, we do an UPDATE first and then a SELECT
        token.ThrowIfCancellationRequested();

        await InternalRefreshCacheItemAsync(key, token).ConfigureAwait(false);

        return await InternalGetCacheItemAsync(key, token).ConfigureAwait(false);
    }

    public void RefreshCacheItem(string key)
    {
        InternalRefreshCacheItem(key);
    }

    public async Task RefreshCacheItemAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        await InternalRefreshCacheItemAsync(key, token).ConfigureAwait(false);
    }

    public void DeleteCacheItem(string key)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.DeleteCacheItem;
        command.Parameters.AddCacheItemId(key);

        command.ExecuteNonQuery();
    }

    public async Task DeleteCacheItemAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        await using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.DeleteCacheItem;
        command.Parameters.AddCacheItemId(key);

        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    public void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (key.Length > OracleParameterCollectionExtensions.CacheItemIdColumnWidth)
            return;

        var utcNow = SystemClock.UtcNow;

        var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
        ValidateOptions(options.SlidingExpiration, absoluteExpiration);

        using var connection = new OracleConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.SetCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddCacheItemValue(value)
            .AddSlidingExpirationInSeconds(options.SlidingExpiration)
            .AddAbsoluteExpiration(absoluteExpiration)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, utcNow);

        try
        {
            command.ExecuteNonQuery();
        }
        catch (OracleException ex)
        {
            if (IsDuplicateKeyException(ex))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }
    }

    public async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (key.Length > OracleParameterCollectionExtensions.CacheItemIdColumnWidth)
            return;

        var utcNow = SystemClock.UtcNow;

        var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
        ValidateOptions(options.SlidingExpiration, absoluteExpiration);

        await using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.SetCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddCacheItemValue(value)
            .AddSlidingExpirationInSeconds(options.SlidingExpiration)
            .AddAbsoluteExpiration(absoluteExpiration)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, utcNow);

        try
        {
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        catch (OracleException ex)
        {
            if (IsDuplicateKeyException(ex))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }
    }

    public void DeleteExpiredCacheItems()
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.DeleteExpiredCacheItems;
        command.Parameters.AddWithValue("UtcNow", OracleDbType.TimeStampTZ, SystemClock.UtcNow);

        command.ExecuteNonQuery();
    }

    private byte[]? InternalGetCacheItem(string key)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.GetCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, SystemClock.UtcNow);

        using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow |
                                                 CommandBehavior.SingleResult);

        return !reader.Read() ? null : reader.GetFieldValue<byte[]>(Columns.Indexes.CacheItemValueIndex);
    }

    private async Task<byte[]?> InternalGetCacheItemAsync(string key, CancellationToken token = default)
    {
        await using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.GetCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, SystemClock.UtcNow);

        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess |
                                CommandBehavior.SingleRow |
                                CommandBehavior.SingleResult,
                token)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;

        return await reader.GetFieldValueAsync<byte[]>(Columns.Indexes.CacheItemValueIndex, token)
            .ConfigureAwait(false);
    }

    private void InternalRefreshCacheItem(string key)
    {
        using var connection = new OracleConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.RefreshCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, SystemClock.UtcNow);

        command.ExecuteNonQuery();
    }

    private async Task InternalRefreshCacheItemAsync(string key,
        CancellationToken token = default)
    {
        // when retrieving an item, we do an UPDATE first and then a SELECT

        await using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        command.BindByName = true;
        command.CommandText = SqlQueries.RefreshCacheItem;
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", OracleDbType.TimeStampTZ, SystemClock.UtcNow);

        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static bool IsDuplicateKeyException(OracleException ex)
    {
        return ex.Errors is not null && ex.Errors.Cast<OracleError>().Any(error => error.Number == DuplicateKeyErrorId);
    }

    private static DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
    {
        DateTimeOffset? absoluteExpiration = null;

        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
        }
        else if (options.AbsoluteExpiration.HasValue)
        {
            if (options.AbsoluteExpiration.Value <= utcNow)
                throw new InvalidOperationException("The absolute expiration value must be in the future.");

            absoluteExpiration = options.AbsoluteExpiration.Value;
        }

        return absoluteExpiration;
    }

    private static void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
    {
        if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
    }
}