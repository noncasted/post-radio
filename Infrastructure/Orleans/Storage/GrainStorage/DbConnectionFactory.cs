using System.Data.Common;
using Npgsql;

namespace Infrastructure.Orleans;

public class DbConnectionFactory
{
    public static DbConnection CreateConnection(string invariantName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(invariantName))
            throw new ArgumentNullException(nameof(invariantName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        var connection = NpgsqlFactory.Instance.CreateConnection();

        if (connection == null)
        {
            throw new InvalidOperationException(
                $"Database provider factory: '{invariantName}' did not return a connection object."
            );
        }

        connection.ConnectionString = connectionString;
        return connection;
    }
}