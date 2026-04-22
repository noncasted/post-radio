using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Common.Extensions;

public static class DbExtensions
{
    public static Task<NpgsqlConnection> GetConnection(this IHostApplicationBuilder builder)
    {
        return builder.Configuration.GetConnection();
    }

    public static async Task<NpgsqlConnection> GetConnection(this IConfigurationManager configuration)
    {
        var dbConnection = configuration.GetSection("postgres").Get<string>();
        var safeGuard = 0;

        while (safeGuard < 10)
        {
            safeGuard++;

            try
            {
                var newConnection = new NpgsqlConnection(dbConnection);
                await newConnection.OpenAsync();
                return newConnection;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to connect to database: {0}", e.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new Exception();
    }

    public static async Task CreateIfNotExists(this NpgsqlConnection connection, string tableName, string createSql)
    {
        if (await connection.IsTableExists(tableName) == true)
            return;

        await using var createCommand = new NpgsqlCommand(createSql, connection);
        await createCommand.ExecuteNonQueryAsync();
    }

    public static async Task<bool> IsTableExists(this NpgsqlConnection connection, string tableName)
    {
        var checkTableQuery = $@"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '{tableName}'
                );";

        await using var checkTableCommand = new NpgsqlCommand(checkTableQuery, connection);
        var result = await checkTableCommand.ExecuteScalarAsync();

        if (result is not bool tableExists)
            throw new Exception("Failed to check if table exists");

        return tableExists;
    }

    public static async Task Truncate(this NpgsqlConnection connection, string tableName)
    {
        if (await connection.IsTableExists(tableName) == false)
            return;

        var truncateQuery = $"TRUNCATE TABLE {tableName};";
        await using var truncateCommand = new NpgsqlCommand(truncateQuery, connection);
        await truncateCommand.ExecuteNonQueryAsync();
    }

    public static async Task Drop(this NpgsqlConnection connection, string tableName)
    {
        if (await connection.IsTableExists(tableName) == false)
            return;

        var truncateQuery = $"DROP TABLE {tableName};";
        await using var truncateCommand = new NpgsqlCommand(truncateQuery, connection);
        await truncateCommand.ExecuteNonQueryAsync();
    }
}