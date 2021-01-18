using System;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class StringExtensions
    {
        internal static Task<SqlConnection> GetConnection(this string connectionString)
        {
            var connection = new SqlConnection(connectionString);
            var connectionTask = connection.OpenAsync();
            Task.WaitAll(connectionTask);

            if (connectionTask.IsFaulted)
            {
                throw new Exception("Connection failure", connectionTask.Exception);
            }

            return Task.FromResult(connection);
        }
    }
}