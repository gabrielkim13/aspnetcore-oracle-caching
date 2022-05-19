using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.Caching.Oracle;

internal static class OracleParameterCollectionExtensions
{
    public const int DefaultValueColumnWidth = 8000;
    public const int CacheItemIdColumnWidth = 449;

    public static OracleParameterCollection AddCacheItemId(this OracleParameterCollection parameters, string value)
    {
        return parameters.AddWithValue(Columns.Names.CacheItemId, OracleDbType.NVarchar2, value);
    }

    public static OracleParameterCollection AddCacheItemValue(this OracleParameterCollection parameters, byte[]? value)
    {
        return parameters.AddWithValue(Columns.Names.CacheItemValue, OracleDbType.Blob, value);
    }

    public static OracleParameterCollection AddSlidingExpirationInSeconds(this OracleParameterCollection parameters,
        TimeSpan? value)
    {
        object secondsOrNull = value.HasValue ? value.Value.TotalSeconds : DBNull.Value;

        return parameters.AddWithValue(Columns.Names.SlidingExpirationInSeconds, OracleDbType.Int64, secondsOrNull);
    }

    public static OracleParameterCollection AddAbsoluteExpiration(this OracleParameterCollection parameters,
        DateTimeOffset? utcTime)
    {
        object valueOrNull = utcTime.HasValue ? utcTime.Value : DBNull.Value;

        return parameters.AddWithValue(Columns.Names.AbsoluteExpiration, OracleDbType.TimeStampTZ, valueOrNull);
    }

    public static OracleParameterCollection AddWithValue(this OracleParameterCollection parameters,
        string parameterName, OracleDbType dbType, object? value)
    {
        parameters.Add(parameterName, dbType, value, ParameterDirection.Input);

        return parameters;
    }
}