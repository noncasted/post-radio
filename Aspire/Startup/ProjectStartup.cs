using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Aspire;

public class ProjectStartup : BackgroundService
{
    public ProjectStartup(
        IHostApplicationLifetime applicationLifetime,
        IConfiguration configuration,
        ILogger<ProjectStartup> logger)
    {
        _applicationLifetime = applicationLifetime;
        _configuration = configuration;
        _logger = logger;
    }

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProjectStartup> _logger;

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        _logger.LogInformation("[Startup] Startup in progress");

        var connectionString = _configuration.GetConnectionString(ConnectionNames.Postgres);

        _logger.LogInformation("[Startup] Connection string 2: {ConnectionString}", connectionString);

        await using var connection = await GetConnection();

        var isStorageExists = await IsTableExists("orleansstorage");

        if (isStorageExists == false)
        {
            var sqlFiles = new[]
            {
                "PostgreSQL-Main.sql",
                "PostgreSQL-Persistence.sql",
                "PostgreSQL-Clustering.sql",
                "PostgreSQL-Clustering-3.7.0.sql"
            };

            foreach (var file in sqlFiles)
            {
                var script = await File.ReadAllTextAsync(file, cancellation);

                _logger.LogInformation("[Startup] Execute db script: {File}", file);

                await using var command = new NpgsqlCommand(script, connection);
                await command.ExecuteNonQueryAsync(cancellation);
            }
        }
        else
        {
            const string membershipTruncateQuery = "TRUNCATE TABLE orleansmembershiptable;";
            await using var membershipTruncateCommand = new NpgsqlCommand(membershipTruncateQuery, connection);
            await membershipTruncateCommand.ExecuteNonQueryAsync(cancellation);
        }

        foreach (var tableName in States.StateTables)
            await CreateGrainStorageTable(tableName);

        _logger.LogInformation("[Startup] Startup completed");
        _applicationLifetime.StopApplication();

        return;

        async Task<NpgsqlConnection> GetConnection()
        {
            var safeGuard = 0;

            while (safeGuard < 10)
            {
                safeGuard++;

                try
                {
                    var newConnection = new NpgsqlConnection(connectionString);
                    await newConnection.OpenAsync(cancellation);
                    return newConnection;
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to connect to database: {Message}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            }

            throw new Exception();
        }

        async Task CreateGrainStorageTable(string tableName)
        {
            if (await IsTableExists(tableName.ToLower()) == true)
                return;

            var script = await File.ReadAllTextAsync("NamedGrainStorage.sql", cancellation);

            script = script.Replace("TABLE_NAME", tableName);

            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync(cancellation);
        }

        async Task<bool> IsTableExists(string tableName)
        {
            var checkTableQuery = $@"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '{tableName}'
                );";

            await using var checkTableCommand = new NpgsqlCommand(checkTableQuery, connection);
            var result = await checkTableCommand.ExecuteScalarAsync(cancellation);

            if (result is not bool tableExists)
                throw new Exception("Failed to check if table exists");

            return tableExists;
        }
    }
}