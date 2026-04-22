using DeploySetup;
using Microsoft.Extensions.Configuration;

Console.WriteLine("[DeploySetup] Starting idempotent database migrations");

var configuration = new ConfigurationManager();

configuration.AddEnvironmentVariables();

var connectionFromCli = args.FirstOrDefault(a => a.StartsWith("--connection="))?.Substring("--connection=".Length);

if (connectionFromCli != null)
{
    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:postgres"] = connectionFromCli,
        ["postgres"] = connectionFromCli,
    });
}

await PostResourcesSetup.Run(configuration);

Console.WriteLine("[DeploySetup] Done");
