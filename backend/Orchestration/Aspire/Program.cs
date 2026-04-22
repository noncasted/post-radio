using Aspire;
using DeploySetup;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projects;
using Silo = Projects.Silo;
using Frontend = Projects.Server;

// AppHost is now dev-only: production runs through docker-compose.yaml + Coolify, which
// does not boot this assembly. Anything that used to read prod-only env (COOLIFY_URL,
// DB_CONNECTION_STRING, ASPIRE_TOKEN) has been dropped — those paths are dead here.

CleanupLogs();

var builder = DistributedApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.local.json", true);

if (configuration.GetSection("Local").GetSection("KillPrevious").Get<bool>())
    ProcessCleanup.Run();

var consoleToken = configuration["ConsoleToken"] ?? "";

var upstream = DbUpstreamFactory.Create(builder, configuration);
var pgbouncer = PgBouncerFactory.Create(builder, upstream);

var dbConnection = $"Host=127.0.0.1;" +
                   $"Port={pgbouncer.Port};" +
                   $"Database={upstream.Database};" +
                   $"Username={upstream.User};" +
                   $"Password={upstream.Password}";

var minio = builder.AddContainer("minio", "minio/minio", "latest")
                   .WithArgs("server", "/data", "--console-address", ":9001")
                   .WithHttpEndpoint(9000, 9000, "api")
                   .WithHttpEndpoint(9001, 9001, "console")
                   .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
                   .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
                   .WithVolume("post-radio-minio-data", "/data")
                   .WithLifetime(ContainerLifetime.Persistent);

var silo = builder.AddProject<Silo>("silo");
silo.WaitFor(pgbouncer.Resource).WaitFor(minio);

var coordinator = builder.AddProject<Coordinator>("coordinator");
var meta = builder.AddProject<MetaGateway>("meta");

var console = builder
              .AddProject<ConsoleGateway>("console")
              .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var frontend = builder.AddProject<Frontend>("frontend")
                      .WithReference(meta);

// Optional SOCKS5 proxy for SoundCloud (read from appsettings.local.json, gitignored).
// AudioServicesStartup runs in every cluster participant (silo, coordinator, meta, console)
// because AddBase() wires AddAudioServices() into every service — each project initialises
// its own SoundCloudClient singleton. Forward the proxy env to all of them so the "Fetch
// songs" action from the console reaches SoundCloud through the tunnel as well.
var audioProxy = configuration["Audio:Socks5Proxy"];

Console.WriteLine(string.IsNullOrWhiteSpace(audioProxy)
    ? "[AppHost] Audio:Socks5Proxy NOT set in appsettings.local.json — child services will run without proxy."
    : $"[AppHost] Audio:Socks5Proxy = '{audioProxy}' — forwarding to silo/coordinator/meta/console.");

foreach (var project in new[] { silo, coordinator, meta, console })
{
    project.WithEnvironment("Minio__Endpoint", "localhost:9000")
           .WithEnvironment("Minio__AccessKey", "minioadmin")
           .WithEnvironment("Minio__SecretKey", "minioadmin");

    if (!string.IsNullOrWhiteSpace(audioProxy))
        project.WithEnvironment("Audio__Socks5Proxy", audioProxy);
}

SetupDB();

coordinator.WaitFor(silo);
meta.WaitFor(silo).WaitFor(coordinator);
console.WaitFor(silo).WaitFor(coordinator);
frontend.WaitFor(meta);

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((_, _) => PostResourcesSetup.Run(configuration));

builder.Build().Run();

return;

void CleanupLogs()
{
    var logsDir = TelemetryPaths.GetTelemetryDir("logs");

    if (logsDir == null)
        return;

    foreach (var entry in Directory.EnumerateFileSystemEntries(logsDir))
    {
        try
        {
            if (File.Exists(entry))
                File.Delete(entry);
            else if (Directory.Exists(entry))
                Directory.Delete(entry, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

void SetupDB()
{
    var projectResources = new[]
    {
        silo,
        coordinator,
        meta,
        console
    };

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["postgres"] = dbConnection,
        ["ConnectionStrings__postgres"] = dbConnection
    });

    foreach (var resource in projectResources)
    {
        resource.WithEnvironment(context => {
            context.EnvironmentVariables["postgres"] = dbConnection;
            context.EnvironmentVariables["ConnectionStrings__postgres"] = dbConnection;
        });
    }
}