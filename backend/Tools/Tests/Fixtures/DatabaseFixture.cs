using Common;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Tests.Fixtures;

/// <summary>
/// Manages a shared PostgreSQL container and per-fixture test databases.
/// Creates state tables from StatesLookup and side effects tables.
/// </summary>
public class DatabaseFixture : IAsyncDisposable
{
    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static PostgreSqlContainer? _sharedContainer;
    private static int _refCount;

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    public string ConnectionString => _connectionString;
    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ContainerLock.WaitAsync();

        try
        {
            if (_sharedContainer == null)
            {
                _sharedContainer = new PostgreSqlBuilder()
                                   .WithImage("postgres:17")
                                   .WithUsername("test")
                                   .WithPassword("test")
                                   .Build();

                await _sharedContainer.StartAsync();
            }

            _refCount++;
        }
        finally
        {
            ContainerLock.Release();
        }

        _databaseName = $"test_db_{Guid.NewGuid():N}";

        // Create the test database
        var masterConnectionString = _sharedContainer!.GetConnectionString();
        await using var masterConnection = new NpgsqlConnection(masterConnectionString);
        await masterConnection.OpenAsync();
        await using var createDbCmd = masterConnection.CreateCommand();
        createDbCmd.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await createDbCmd.ExecuteNonQueryAsync();

        _connectionString = new NpgsqlConnectionStringBuilder(masterConnectionString)
        {
            Database = _databaseName
        }.ConnectionString;

        DataSource = NpgsqlDataSource.Create(_connectionString);

        await InitializeSchema();
    }

    private async Task InitializeSchema()
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        // Create state tables from StatesLookup — matching production schema (StatesSetup.cs)
        var createdTables = new HashSet<string>();

        foreach (var info in StatesLookup.All)
        {
            if (!createdTables.Add(info.TableName))
                continue;

            var (keyDef, indexDef) = info.KeyType switch
            {
                GrainKeyType.Integer => ("bigint not null", "(key, type)"),
                GrainKeyType.String => ("character varying(512) not null", "(key, type)"),
                GrainKeyType.Guid => ("uuid not null", "(key, type)"),
                GrainKeyType.IntegerAndString => ("bigint not null, extension character varying(512) not null",
                    "(key, type, extension)"),
                GrainKeyType.GuidAndString => ("uuid not null, extension character varying(512) not null",
                    "(key, type, extension)"),
                _ => throw new ArgumentOutOfRangeException()
            };

            await using var cmd = connection.CreateCommand();

            cmd.CommandText = $"""
                               CREATE TABLE {info.TableName} (
                                   key {keyDef},
                                   type character varying(512) not null,
                                   value jsonb NOT NULL,
                                   version int NOT NULL,
                                   primary key {indexDef}
                               );
                               CREATE INDEX ix_{info.TableName} ON {info.TableName} USING btree {indexDef};
                               """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Create side effects tables
        await using var seCmd = connection.CreateCommand();

        seCmd.CommandText = """
                            CREATE TABLE IF NOT EXISTS side_effects_queue (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL DEFAULT now()
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_processing (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                processing_started_at timestamptz NOT NULL DEFAULT now()
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_retry_queue (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                retry_after timestamptz NOT NULL
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_dead_letter (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                failed_at timestamptz NOT NULL DEFAULT now(),
                                error_message text
                            );
                            """;
        await seCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Truncate all state and side effects tables for per-test isolation.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              DO $$
                              DECLARE
                                  tbl text;
                              BEGIN
                                  FOR tbl IN
                                      SELECT tablename FROM pg_tables
                                      WHERE schemaname = 'public'
                                  LOOP
                                      EXECUTE format('TRUNCATE TABLE %I RESTART IDENTITY CASCADE', tbl);
                                  END LOOP;
                              END
                              $$;
                              """;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Delete specific state records by table name and key.
    /// </summary>
    public async Task DeleteStateAsync(string tableName, string key)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""DELETE FROM "{tableName}" WHERE key = @key""";
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Check if there are pending side effects.
    /// </summary>
    public async Task<bool> HasPendingSideEffectsAsync()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT (SELECT count(*) FROM side_effects_queue) +
                                     (SELECT count(*) FROM side_effects_processing) +
                                     (SELECT count(*) FROM side_effects_retry_queue)
                              """;

        var result = await command.ExecuteScalarAsync();
        return result is long count && count > 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (DataSource != null!)
            await DataSource.DisposeAsync();

        await ContainerLock.WaitAsync();

        try
        {
            _refCount--;

            if (_refCount <= 0 && _sharedContainer != null)
            {
                await _sharedContainer.DisposeAsync();
                _sharedContainer = null;
            }
        }
        finally
        {
            ContainerLock.Release();
        }
    }
}