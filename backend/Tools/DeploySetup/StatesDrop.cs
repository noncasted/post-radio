using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public class StatesDrop
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in StatesLookup.All)
            await connection.Drop(info.TableName);

        await connection.Drop(DbLookup.SE_Queue);
        await connection.Drop(DbLookup.SE_Processing);
        await connection.Drop(DbLookup.SE_Retry);

        // Benchmark data now in state_benchmark (included in StatesLookup.All)
    }
}