using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire;

public record PgBouncerResult
{
    public required int Port { get; init; }
    public required IResourceBuilder<ContainerResource> Resource { get; init; }
}

// Dev-only sidecar pooling in front of the Aspire-managed Postgres container.
// In production pgbouncer is a first-class compose service, not driven from here.
public static class PgBouncerFactory
{
    public static PgBouncerResult Create(IDistributedApplicationBuilder builder, DbUpstream db)
    {
        var pgbouncerPort = db.Port + 1;
        var pgbouncerDir = Path.Combine(builder.AppHostDirectory, "ContainersData/PgBouncer");
        var pgbouncerConfigPath = Path.Combine(pgbouncerDir, "pgbouncer.ini");
        var pgbouncerUserlistPath = Path.Combine(pgbouncerDir, "userlist.txt");
        var databasesIniPath = Path.Combine(pgbouncerDir, "databases.ini");

        File.WriteAllText(databasesIniPath,
            $"[databases]\n* = host={db.Host} port={db.Port}\n");

        File.WriteAllText(pgbouncerUserlistPath,
            $"\"{db.User}\" \"{db.Password}\"\n");

        const string pgbouncerHealthCheckName = "pgbouncer-tcp";

        builder.Services.AddHealthChecks().AddAsyncCheck(pgbouncerHealthCheckName, async () => {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await client.ConnectAsync("127.0.0.1", pgbouncerPort, cts.Token);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"PgBouncer not listening on {pgbouncerPort}: {ex.Message}");
            }
        });

        var pgbouncer = builder.AddContainer("pgbouncer", "edoburu/pgbouncer", "latest")
                               .WithHttpEndpoint(port: pgbouncerPort, targetPort: 6432, name: "pgbouncer-port",
                                   isProxied: false)
                               .WithBindMount(pgbouncerConfigPath, "/etc/pgbouncer/pgbouncer.ini", isReadOnly: true)
                               .WithBindMount(databasesIniPath, "/etc/pgbouncer/databases.ini", isReadOnly: true)
                               .WithBindMount(pgbouncerUserlistPath, "/etc/pgbouncer/userlist.txt", isReadOnly: true)
                               .WithHealthCheck(pgbouncerHealthCheckName)
                               .WithLifetime(ContainerLifetime.Persistent)
                               .WaitFor(db.PostgresResource);

        return new PgBouncerResult { Port = pgbouncerPort, Resource = pgbouncer };
    }
}