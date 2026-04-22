using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public static class BenchmarkSetup
{
    public static Task Run(IConfigurationManager configuration)
    {
        // Benchmarks now use the state system (state_benchmark table)
        // No custom table setup needed
        return Task.CompletedTask;
    }
}