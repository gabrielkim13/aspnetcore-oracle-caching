using System.Data;

using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.Caching.Oracle;

internal static class OracleParameterCollectionExtensions
{
    public const int DefaultValueColumnWidth = 8000;
    public const int CacheItemIdColumnWidth = 449;

    extension(OracleParameterCollection parameters)
    {
        public OracleParameterCollection AddCacheItemId(string value)
        {
            return parameters.AddWithValue(Columns.Names.CacheItemId, OracleDbType.NVarchar2, value);
        }

        public OracleParameterCollection AddCacheItemValue(byte[]? value)
        {
            return parameters.AddWithValue(Columns.Names.CacheItemValue, OracleDbType.Blob, value);
        }

        public OracleParameterCollection AddSlidingExpirationInSeconds(TimeSpan? value)
        {
            object secondsOrNull = value.HasValue ? value.Value.TotalSeconds : DBNull.Value;

            return parameters.AddWithValue(Columns.Names.SlidingExpirationInSeconds, OracleDbType.Int64, secondsOrNull);
        }

        public OracleParameterCollection AddAbsoluteExpiration(DateTimeOffset? utcTime)
        {
            object valueOrNull = utcTime.HasValue ? utcTime.Value : DBNull.Value;

            return parameters.AddWithValue(Columns.Names.AbsoluteExpiration, OracleDbType.TimeStampTZ, valueOrNull);
        }

        public OracleParameterCollection AddWithValue(string parameterName, OracleDbType dbType, object? value)
        {
            parameters.Add(parameterName, dbType, value, ParameterDirection.Input);

            return parameters;
        }
    }
}
