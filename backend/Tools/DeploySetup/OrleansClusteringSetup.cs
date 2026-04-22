using System.Reflection;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DeploySetup;

// Bootstraps the schema required by Orleans AdoNet clustering provider (OrleansQuery,
// OrleansMembershipVersionTable, OrleansMembershipTable + helper functions). Upstream
// scripts are not idempotent, so we gate on the OrleansQuery table already existing.
public static class OrleansClusteringSetup
{
    private const string MarkerTable = "orleansquery";

    // Full bootstrap only runs once (gated on MarkerTable). Supplemental runs every
    // time because it is already idempotent and patches missing rows on existing DBs.
    private static readonly string[] BootstrapResources =
    [
        "DeploySetup.Sql.PostgreSQL-Main.sql",
        "DeploySetup.Sql.PostgreSQL-Clustering.sql"
    ];

    private const string SupplementalResource = "DeploySetup.Sql.PostgreSQL-Supplemental.sql";

    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        if (!await connection.IsTableExists(MarkerTable))
        {
            foreach (var resourceName in BootstrapResources)
                await ExecuteEmbeddedSql(connection, resourceName);
        }

        await ExecuteEmbeddedSql(connection, SupplementalResource);
    }

    private static async Task ExecuteEmbeddedSql(NpgsqlConnection connection, string resourceName)
    {
        var sql = await ReadEmbeddedResource(resourceName);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadEmbeddedResource(string resourceName)
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                                 ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
