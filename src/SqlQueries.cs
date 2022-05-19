using System.Globalization;

namespace Microsoft.Extensions.Caching.Oracle;

internal sealed class SqlQueries
{
    private const string TableInfoFormat = @"
        SELECT TABLESPACE_NAME, OWNER, TABLE_NAME, TABLE_TYPE 
        FROM ALL_ALL_TABLES 
        WHERE OWNER = UPPER('{0}') 
        AND TABLE_NAME = UPPER('{1}')
    ";

    private const string RefreshCacheItemFormat = @"
        UPDATE
            {0}
        SET ExpiresAtTime =
                (CASE
                     WHEN TO_NUMBER(24 * 60 * 60 * (CAST(AbsoluteExpiration AS DATE) - CAST(:UtcNow AS DATE))) <=
                          SlidingExpirationInSeconds
                         THEN AbsoluteExpiration
                     ELSE
                         :UtcNow + NUMTODSINTERVAL(SlidingExpirationInSeconds, 'SECOND')
                    END)
        WHERE Id = :Id
          AND :UtcNow <= ExpiresAtTime
          AND SlidingExpirationInSeconds IS NOT NULL
          AND (AbsoluteExpiration IS NULL OR AbsoluteExpiration <> ExpiresAtTime)
            ";

    private const string GetCacheItemFormat = @"
        SELECT Value
        FROM {0}
        WHERE Id = :Id
          AND :UtcNow <= ExpiresAtTime
    ";

    private const string SetCacheItemFormat = @"
        MERGE INTO {0} x
        USING (SELECT :Id                                                                           AS Id,
                      :Value                                                                        AS Value,
                      CASE
                          WHEN :SlidingExpirationInSeconds IS NULL THEN :AbsoluteExpiration
                          ELSE :UtcNow + NUMTODSINTERVAL(:SlidingExpirationInSeconds, 'SECOND') END AS ExpiresAtTime,
                      :SlidingExpirationInSeconds                                                   AS SlidingExpirationInSeconds,
                      :AbsoluteExpiration                                                           AS AbsoluteExpiration
               FROM DUAL) y
        ON (x.Id = y.Id)
        WHEN MATCHED THEN
            UPDATE
            SET x.Value                      = y.Value,
                x.ExpiresAtTime              = y.ExpiresAtTime,
                x.SlidingExpirationInSeconds = y.SlidingExpirationInSeconds,
                x.AbsoluteExpiration         = y.AbsoluteExpiration
            WHERE x.Id = y.Id
        WHEN NOT MATCHED THEN
            INSERT (x.Id, x.Value, x.ExpiresAtTime, x.SlidingExpirationInSeconds, x.AbsoluteExpiration)
            VALUES (y.Id, y.Value, y.ExpiresAtTime, y.SlidingExpirationInSeconds, y.AbsoluteExpiration)
    ";

    private const string DeleteCacheItemFormat = @"
        DELETE
        FROM {0}
        WHERE Id = :Id
    ";

    public const string DeleteExpiredCacheItemsFormat = @"
        DELETE 
        FROM {0} 
        WHERE :UtcNow > ExpiresAtTime
    ";

    public SqlQueries(string schemaName, string tableName)
    {
        var tableNameWithSchema = string.Format(
            CultureInfo.InvariantCulture,
            "{0}.{1}",
            DelimitIdentifier(schemaName), DelimitIdentifier(tableName)
        );

        GetCacheItem = string.Format(CultureInfo.InvariantCulture, GetCacheItemFormat, tableNameWithSchema);

        RefreshCacheItem = string.Format(CultureInfo.InvariantCulture, RefreshCacheItemFormat, tableNameWithSchema);

        DeleteCacheItem = string.Format(CultureInfo.InvariantCulture, DeleteCacheItemFormat, tableNameWithSchema);

        DeleteExpiredCacheItems = string.Format(
            CultureInfo.InvariantCulture,
            DeleteExpiredCacheItemsFormat,
            tableNameWithSchema
        );

        SetCacheItem = string.Format(CultureInfo.InvariantCulture, SetCacheItemFormat, tableNameWithSchema);

        TableInfo = string.Format(
            CultureInfo.InvariantCulture,
            TableInfoFormat,
            EscapeLiteral(schemaName), EscapeLiteral(tableName)
        );
    }

    public string TableInfo { get; }

    public string GetCacheItem { get; }

    public string RefreshCacheItem { get; }

    public string SetCacheItem { get; }

    public string DeleteCacheItem { get; }

    public string DeleteExpiredCacheItems { get; }

    private static string DelimitIdentifier(string identifier)
    {
        return identifier;
    }

    private static string EscapeLiteral(string literal)
    {
        return literal.Replace("'", "''");
    }
}