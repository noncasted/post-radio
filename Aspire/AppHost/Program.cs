using Microsoft.Extensions.Configuration;
using Projects;
using Console = Projects.Console;

var builder = DistributedApplication.CreateBuilder(args);

var startup = builder.AddProject<Startup>("startup");
var silo = builder.AddProject<Silo>("silo");
var coordinator = builder.AddProject<Coordinator>("coordinator");
var frontend = builder.AddProject<Frontend>("frontend");
var console = builder.AddProject<Console>("console");

var projectResources = new[]
{
    startup,
    silo,
    coordinator,
    frontend,
    console
};

SetupDB();
SetupS3();
SetupAuthToken();

silo.WaitForCompletion(startup);
coordinator.WaitFor(silo);
frontend.WaitFor(silo);
console.WaitFor(silo);

SetDashboardToken();

builder.Build().Run();

return;

void SetupDB()
{
    var externalDb = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    if (externalDb == null)
        externalDb = builder.Configuration.GetConnectionString("db");

    foreach (var resource in projectResources)
        resource.WithEnvironment(context => context.EnvironmentVariables["ConnectionStrings__postgres"] = externalDb);
}

void SetupS3()
{
    var endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
    var accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY");
    var secretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY");

    if (endpoint == null || accessKey == null || secretKey == null)
    {
        builder.Configuration.AddJsonFile("appsettings.secrets.json");
        var section = builder.Configuration.GetSection("MinioCredentials");
        endpoint = section.GetSection("Endpoint").Get<string>();
        accessKey = section.GetSection("AccessKey").Get<string>();
        secretKey = section.GetSection("SecretKey").Get<string>();
    }

    foreach (var resource in projectResources)
    {
        resource.WithEnvironment(context => context.EnvironmentVariables["MINIO_ENDPOINT"] = endpoint!);
        resource.WithEnvironment(context => context.EnvironmentVariables["MINIO_ACCESSKEY"] = accessKey!);
        resource.WithEnvironment(context => context.EnvironmentVariables["MINIO_SECRETKEY"] = secretKey!);
    }
}

void SetupAuthToken()
{
    var authToken = Environment.GetEnvironmentVariable("AUTH_TOKEN");

    if (authToken == null)
    {
        builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true);
        authToken = builder.Configuration.GetSection("AuthToken").Get<string>();
    }

    if (!string.IsNullOrEmpty(authToken))
    {
        console.WithEnvironment(context => context.EnvironmentVariables["AuthToken"] = authToken);
    }
}

void SetDashboardToken()
{
    var token = Environment.GetEnvironmentVariable("ASPIRE_TOKEN");

    if (token == null)
        return;

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppHost:BrowserToken"] = token,
        }
    );
}