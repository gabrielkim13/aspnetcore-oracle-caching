using System.Globalization;

namespace Microsoft.Extensions.Caching.Oracle;

internal sealed class SqlQueries
{
    private const string TableInfoFormat =
    """
        SELECT TABLESPACE_NAME, OWNER, TABLE_NAME, TABLE_TYPE 
        FROM ALL_ALL_TABLES 
        WHERE OWNER = '{0}' 
        AND TABLE_NAME = '{1}'
        """;

    private const string RefreshCacheItemFormat =
        """
        UPDATE
            {0}
        SET "EXPIRESATTIME" =
                (CASE
                     WHEN TO_NUMBER(24 * 60 * 60 * (CAST("ABSOLUTEEXPIRATION" AS DATE) - CAST(:UtcNow AS DATE))) <=
                          "SLIDINGEXPIRATIONINSECONDS"
                         THEN "ABSOLUTEEXPIRATION"
                     ELSE
                         :UtcNow + NUMTODSINTERVAL("SLIDINGEXPIRATIONINSECONDS", 'SECOND')
                    END)
        WHERE "ID" = :Id
          AND :UtcNow <= "EXPIRESATTIME"
          AND "SLIDINGEXPIRATIONINSECONDS" IS NOT NULL
          AND ("ABSOLUTEEXPIRATION" IS NULL OR "ABSOLUTEEXPIRATION" != "EXPIRESATTIME")
        """;

    private const string GetCacheItemFormat =
        """
        SELECT "VALUE"
        FROM {0}
        WHERE "ID" = :Id
          AND :UtcNow <= "EXPIRESATTIME"
        """;

    private const string SetCacheItemFormat =
        """
        MERGE INTO {0} x
        USING (SELECT :Id                                                                           AS "ID",
                      :Value                                                                        AS "VALUE",
                      CASE
                          WHEN :SlidingExpirationInSeconds IS NULL THEN :AbsoluteExpiration
                          ELSE :UtcNow + NUMTODSINTERVAL(:SlidingExpirationInSeconds, 'SECOND') END AS "EXPIRESATTIME",
                      :SlidingExpirationInSeconds                                                   AS "SLIDINGEXPIRATIONINSECONDS",
                      :AbsoluteExpiration                                                           AS "ABSOLUTEEXPIRATION"
               FROM DUAL) y
        ON (x."ID" = y."ID")
        WHEN MATCHED THEN
            UPDATE
            SET x."VALUE"                      = y."VALUE",
                x."EXPIRESATTIME"              = y."EXPIRESATTIME",
                x."SLIDINGEXPIRATIONINSECONDS" = y."SLIDINGEXPIRATIONINSECONDS",
                x."ABSOLUTEEXPIRATION"         = y."ABSOLUTEEXPIRATION"
            WHERE x."ID" = y."ID"
        WHEN NOT MATCHED THEN
            INSERT (x."ID", x."VALUE", x."EXPIRESATTIME", x."SLIDINGEXPIRATIONINSECONDS", x."ABSOLUTEEXPIRATION")
            VALUES (y."ID", y."VALUE", y."EXPIRESATTIME", y."SLIDINGEXPIRATIONINSECONDS", y."ABSOLUTEEXPIRATION")
        """;

    private const string DeleteCacheItemFormat =
        """
        DELETE
        FROM {0}
        WHERE "ID" = :Id
        """;

    private const string DeleteExpiredCacheItemsFormat =
        """
        DELETE 
        FROM {0}
        WHERE :UtcNow > "EXPIRESATTIME"
        """;

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

        DeleteExpiredCacheItems =
            string.Format(CultureInfo.InvariantCulture, DeleteExpiredCacheItemsFormat, tableNameWithSchema);

        SetCacheItem = string.Format(CultureInfo.InvariantCulture, SetCacheItemFormat, tableNameWithSchema);

        TableInfo = string.Format(CultureInfo.InvariantCulture, TableInfoFormat, EscapeLiteral(schemaName),
            EscapeLiteral(tableName));
    }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
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
