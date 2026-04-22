using Common;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure;

public class SideEffectEntry
{
    public required Guid Id { get; init; }
    public required ISideEffect Effect { get; init; }
    public required int RetryCount { get; init; }
}

public class SideEffectsStats
{
    public int QueueCount { get; init; }
    public int ProcessingCount { get; init; }
    public int RetryCount { get; init; }
    public int DeadLetterCount { get; init; }
}

public class RetryQueueEntry
{
    public required Guid Id { get; init; }
    public required string TypeName { get; init; }
    public required int RetryCount { get; init; }
    public required DateTime RetryAfter { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public interface ISideEffectsStorage
{
    Task Write(ISideEffect effects);

    // Write new side effects into side_effects_queue (called within Transactions.Process() pgTransaction)
    Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects);

    // Atomically move up to `count` oldest entries from queue → processing. Returns them.
    Task<IReadOnlyList<SideEffectEntry>> Read(int count);

    // Delete from side_effects_processing within an existing Postgres transaction (for ITransactionalSideEffect)
    Task CompleteProcessing(NpgsqlTransaction transaction, Guid id);

    // Delete from side_effects_processing standalone (for simple ISideEffect)
    Task CompleteProcessing(Guid id);

    // Move from side_effects_processing → side_effects_retry_queue (or dead letter if max retries exceeded)
    Task FailProcessing(
        Guid id,
        int retryCount,
        int maxRetryCount,
        float incrementalRetryDelaySeconds,
        string? errorMessage = null);

    // Move entries from side_effects_retry_queue → side_effects_queue where retry_after <= now
    Task RequeueReady();

    // At startup: move everything from side_effects_processing back to side_effects_queue
    Task RequeueStuck();

    // Periodically: move entries stuck in processing longer than `age` back to queue
    Task RequeueStuckOlderThan(TimeSpan age);

    // Monitor: get counts for all three tables
    Task<SideEffectsStats> GetStats();

    // Monitor: get entries from retry queue for display
    Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit);

    // Monitor: drop a single entry from retry queue
    Task DropRetryEntry(Guid id);

    // Monitor: move a single entry from retry queue back to main queue
    Task RequeueRetryEntry(Guid id);
}

public class SideEffectsStorage : ISideEffectsStorage
{
    public SideEffectsStorage(IDbSource dbSource, IStateSerializer serializer, ILogger<SideEffectsStorage> logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly ILogger<SideEffectsStorage> _logger;

    public async Task Write(ISideEffect effects)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await Write(transaction, [effects]);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffectsStorage] Failed to write side effect");
            await transaction.RollbackAsync();
        }
    }

    public async Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects)
    {
        if (effects.Count == 0)
            return;

        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;

        var values = new List<string>(effects.Count);

        for (var i = 0; i < effects.Count; i++)
        {
            var payload = _serializer.Serialize(effects[i]);

            var idParam = $"id{i}";
            var payloadParam = $"payload{i}";

            command.Parameters.AddWithValue(idParam, Guid.NewGuid());
            var p = command.Parameters.AddWithValue(payloadParam, payload);
            p.NpgsqlDbType = NpgsqlDbType.Jsonb;

            values.Add($"(@{idParam}, @{payloadParam}::jsonb, 0, now())");
        }

        command.CommandText = $@"
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            VALUES {string.Join(", ", values)}
        ";

        await command.ExecuteNonQueryAsync();
    }

    // Atomically move oldest `count` entries from queue to processing and return them.
    // Uses CTE to prevent concurrent workers from picking the same entries.
    public async Task<IReadOnlyList<SideEffectEntry>> Read(int count)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH picked AS (
                SELECT id, payload, retry_count, created_at
                FROM side_effects_queue
                ORDER BY created_at
                LIMIT @count
                FOR UPDATE SKIP LOCKED
            ),
            inserted AS (
                INSERT INTO side_effects_processing (id, payload, retry_count, created_at, processing_started_at)
                SELECT id, payload, retry_count, created_at, now()
                FROM picked
            ),
            deleted AS (
                DELETE FROM side_effects_queue
                WHERE id IN (SELECT id FROM picked)
            )
            SELECT id, payload::text, retry_count FROM picked
        ";
        command.Parameters.AddWithValue("count", count);

        var entries = new List<SideEffectEntry>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetGuid(0);
            var payloadJson = reader.GetString(1);
            var retryCount = reader.GetInt32(2);

            try
            {
                var effect = _serializer.Deserialize<ISideEffect>(payloadJson);
                entries.Add(new SideEffectEntry { Id = id, Effect = effect, RetryCount = retryCount });
            }
            catch (Exception)
            {
                // If deserialization fails, the entry stays in processing and will be failed later.
                entries.Add(new SideEffectEntry
                {
                    Id = id,
                    Effect = new DeadLetterSideEffect(),
                    RetryCount = retryCount
                });
            }
        }

        return entries;
    }

    public async Task CompleteProcessing(NpgsqlTransaction transaction, Guid id)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CompleteProcessing(Guid id)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task FailProcessing(
        Guid id,
        int retryCount,
        int maxRetryCount,
        float incrementalRetryDelaySeconds,
        string? errorMessage = null)
    {
        if (retryCount >= maxRetryCount)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var deadLetterTx = await connection.BeginTransactionAsync();

            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = deadLetterTx;

            insertCmd.CommandText = $@"
                INSERT INTO {DbLookup.SE_DeadLetter} (id, payload, retry_count, created_at, failed_at, error_message)
                SELECT id, payload, retry_count, created_at, now(), @errorMessage
                FROM side_effects_processing
                WHERE id = @id
                ON CONFLICT (id) DO NOTHING
            ";
            insertCmd.Parameters.AddWithValue("id", id);
            insertCmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();

            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = deadLetterTx;
            deleteCmd.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
            deleteCmd.Parameters.AddWithValue("id", id);
            await deleteCmd.ExecuteNonQueryAsync();

            await deadLetterTx.CommitAsync();

            BackendMetrics.SideEffectDeadLetter.Add(1);

            _logger.LogError("[SideEffects] Effect {Id} moved to dead letter after {RetryCount} retries: {Error}",
                id, retryCount, errorMessage);
            return;
        }

        var delaySeconds = (retryCount + 1) * incrementalRetryDelaySeconds;
        var retryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);
        var newRetryCount = retryCount + 1;

        await using var conn = await _dbSource.Value.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var insertCommand = conn.CreateCommand();
        insertCommand.Transaction = tx;

        insertCommand.CommandText = @"
            INSERT INTO side_effects_retry_queue (id, payload, retry_count, created_at, retry_after)
            SELECT id, payload, @newRetryCount, created_at, @retryAfter
            FROM side_effects_processing
            WHERE id = @id
        ";
        insertCommand.Parameters.AddWithValue("id", id);
        insertCommand.Parameters.AddWithValue("newRetryCount", newRetryCount);
        insertCommand.Parameters.AddWithValue("retryAfter", retryAfter);
        await insertCommand.ExecuteNonQueryAsync();

        await using var deleteCommand = conn.CreateCommand();
        deleteCommand.Transaction = tx;
        deleteCommand.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        deleteCommand.Parameters.AddWithValue("id", id);
        await deleteCommand.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    public async Task RequeueStuck()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH stuck AS (
                DELETE FROM side_effects_processing
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM stuck
            ON CONFLICT (id) DO NOTHING
        ";
        await command.ExecuteNonQueryAsync();
    }

    public async Task RequeueStuckOlderThan(TimeSpan age)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH stuck AS (
                DELETE FROM side_effects_processing
                WHERE processing_started_at < @cutoff
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM stuck
            ON CONFLICT (id) DO NOTHING
        ";
        command.Parameters.AddWithValue("cutoff", DateTime.UtcNow - age);

        var moved = await command.ExecuteNonQueryAsync();

        if (moved > 0)
            _logger.LogWarning("[SideEffectsStorage] Requeued {Count} stuck entries older than {Age}", moved, age);
    }

    public async Task RequeueReady()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH ready AS (
                DELETE FROM side_effects_retry_queue
                WHERE retry_after <= now()
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM ready
            ON CONFLICT (id) DO NOTHING
        ";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<SideEffectsStats> GetStats()
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();

            command.CommandText = $@"
                SELECT
                    (SELECT COUNT(*)::int FROM side_effects_queue) AS queue_count,
                    (SELECT COUNT(*)::int FROM side_effects_processing) AS processing_count,
                    (SELECT COUNT(*)::int FROM side_effects_retry_queue) AS retry_count,
                    (SELECT COUNT(*)::int FROM {DbLookup.SE_DeadLetter}) AS dead_letter_count
            ";

            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            return new SideEffectsStats
            {
                QueueCount = reader.GetInt32(0),
                ProcessingCount = reader.GetInt32(1),
                RetryCount = reader.GetInt32(2),
                DeadLetterCount = reader.GetInt32(3)
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffectsStorage] Failed to get stats");
            return new SideEffectsStats();
        }
    }

    public async Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT id, payload::text, retry_count, retry_after, created_at
                FROM side_effects_retry_queue
                ORDER BY retry_after
                LIMIT @limit
            ";
            command.Parameters.AddWithValue("limit", limit);

            var entries = new List<RetryQueueEntry>();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var payloadJson = reader.GetString(1);
                var typeName = ExtractTypeName(payloadJson);

                entries.Add(new RetryQueueEntry
                {
                    Id = reader.GetGuid(0),
                    TypeName = typeName,
                    RetryCount = reader.GetInt32(2),
                    RetryAfter = reader.GetDateTime(3),
                    CreatedAt = reader.GetDateTime(4)
                });
            }

            return entries;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffectsStorage] Failed to get retry entries");
            return Array.Empty<RetryQueueEntry>();
        }
    }

    public async Task DropRetryEntry(Guid id)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM side_effects_retry_queue WHERE id = @id";
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffectsStorage] Failed to drop retry entry {Id}", id);
        }
    }

    public async Task RequeueRetryEntry(Guid id)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var tx = await connection.BeginTransactionAsync();

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = tx;

            insertCommand.CommandText = @"
                INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
                SELECT id, payload, retry_count, created_at
                FROM side_effects_retry_queue
                WHERE id = @id
                ON CONFLICT (id) DO NOTHING
            ";
            insertCommand.Parameters.AddWithValue("id", id);
            await insertCommand.ExecuteNonQueryAsync();

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = tx;
            deleteCommand.CommandText = "DELETE FROM side_effects_retry_queue WHERE id = @id";
            deleteCommand.Parameters.AddWithValue("id", id);
            await deleteCommand.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffectsStorage] Failed to requeue retry entry {Id}", id);
        }
    }

    private static string ExtractTypeName(string payloadJson)
    {
        try
        {
            var typeStart = payloadJson.IndexOf("\"$type\"", StringComparison.Ordinal);

            if (typeStart < 0)
                return "Unknown";

            var valueStart = payloadJson.IndexOf('"', typeStart + 7) + 1;
            var valueEnd = payloadJson.IndexOf('"', valueStart);

            if (valueStart <= 0 || valueEnd < 0)
                return "Unknown";

            var fullType = payloadJson[valueStart..valueEnd];
            var lastDot = fullType.LastIndexOf('.');
            return lastDot >= 0 ? fullType[(lastDot + 1)..] : fullType;
        }
        catch
        {
            return "Unknown";
        }
    }
}

// Sentinel used when payload deserialization fails — immediately fails without executing.
internal class DeadLetterSideEffect : ISideEffect
{
    public Task Execute(IOrleans orleans) =>
        Task.FromException(new InvalidOperationException("[SideEffects] Dead letter: failed to deserialize payload."));
}