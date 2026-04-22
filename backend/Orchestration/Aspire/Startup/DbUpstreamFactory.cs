using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Aspire;

public record DbUpstream
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public required IResourceBuilder<ContainerResource> PostgresResource { get; init; }
}

// Dev-only: AppHost spins up a local Postgres container for `aspire run`. Production uses
// an external Postgres via docker-compose.yaml — AppHost never runs there.
public static class DbUpstreamFactory
{
    public static DbUpstream Create(IDistributedApplicationBuilder builder, IConfigurationManager configuration)
    {
        var localDb = configuration.GetConnectionString("db").ThrowIfNull();
        var local = ParseConnString(localDb);

        var port = int.Parse(local["Port"]);
        var database = local["Database"];
        var user = local["User Id"];
        var password = local["Password"];

        var postgres = builder
                       .AddContainer("postgres", "postgres", "17.6")
                       .WithHttpEndpoint(port: port, targetPort: 5432, name: "tcp", isProxied: false)
                       .WithVolume("mines-leader-postgres-data", "/var/lib/postgresql/data")
                       .WithEnvironment("POSTGRES_PASSWORD", password)
                       .WithEnvironment("POSTGRES_DB", database)
                       .WithEnvironment("POSTGRES_USER", user)
                       .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
                       .WithLifetime(ContainerLifetime.Persistent);

        return new DbUpstream
        {
            Host = "postgres",
            Port = 5432,
            Database = database,
            User = user,
            Password = password,
            PostgresResource = postgres
        };
    }

    public static Dictionary<string, string> ParseConnString(string connString)
    {
        return connString
               .Split(';', StringSplitOptions.RemoveEmptyEntries)
               .Select(p => p.Split('=', 2))
               .Where(p => p.Length == 2)
               .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}