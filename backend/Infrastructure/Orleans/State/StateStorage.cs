using System.Text;
using Common;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.State;

public class StateIdentity
{
    public required object Key { get; init; }
    public required string Type { get; init; }
    public required string TableName { get; init; }
    public required string? Extension { get; init; }
}

public class GrainStateRecord
{
    public required GrainId Id { get; init; }
    public required IStateValue Value { get; init; }
}

public class StateWriteRequest
{
    public required IReadOnlyDictionary<StateIdentity, IStateValue> Records { get; init; }
    public NpgsqlTransaction? Transaction { get; init; }
}

public class StateDeleteRequest
{
    public required IReadOnlyList<StateIdentity> Identities { get; init; }
}

public interface IStateStorage
{
    IGrainStatesRegistry Registry { get; }

    Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new();

    Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : IStateValue, new();

    IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TValue : IStateValue, new();

    Task Write(StateWriteRequest request);
    Task Delete(StateDeleteRequest request);

    Task<string> ReadRawJson(StateIdentity identity);
}

public class StateStorage : IStateStorage
{
    public StateStorage(
        IGrainStatesRegistry statesRegistry,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations,
        ILogger<StateStorage> logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _migrations = migrations;
        _logger = logger;
        _cache = new StateStorageCache();
        Registry = statesRegistry;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly IStateMigrations _migrations;
    private readonly ILogger<StateStorage> _logger;
    private readonly StateStorageCache _cache;

    public IGrainStatesRegistry Registry { get; }

    public async Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new()
    {
        using var watch = MetricWatch.Start(BackendMetrics.StateReadDuration);

        try
        {
            var (raw, version) = await ReadRaw(identity);

            if (raw == string.Empty)
                return new T();

            var latestVersion = _migrations.GetLatestVersion<T>();

            if (version < latestVersion)
                return _migrations.Migrate<T>(raw, version);

            return _serializer.TryDeserialize<T>(raw).ThrowIfNull();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to read {Type} key={Key} type={StateType}",
                typeof(T).Name, identity.Key, identity.Type);

            throw;
        }
        finally
        {
            BackendMetrics.StateReadTotal.Add(1);
        }
    }

    public async Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(
        IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : IStateValue, new()
    {
        if (identities.Count == 0)
            return new Dictionary<TKey, TValue>();

        var stateInfo = Registry.Get<TValue>();
        var latestVersion = _migrations.GetLatestVersion<TValue>();
        var result = new Dictionary<TKey, TValue>(identities.Count);

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            foreach (var group in identities.GroupBy(i => i.Extension))
            {
                var groupList = group.ToList();
                var hasExtension = group.Key != null;

                await using var command = connection.CreateCommand();
                command.CommandText = _cache.GetReadBatchQuery(groupList.First());
                command.Parameters.AddWithValue("type", stateInfo.Name);
                command.Parameters.AddWithValue("keys", BuildKeysArray(groupList, stateInfo.KeyType));

                if (hasExtension)
                    command.Parameters.AddWithValue("extension", group.Key!);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var key = ReadKeyFromReader<TKey>(reader, stateInfo);

                    var payloadBinary = reader.GetFieldValue<byte[]>(1);
                    var version = reader.GetFieldValue<int>(2);

                    var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
                    var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

                    result[key] = version < latestVersion
                        ? _migrations.Migrate<TValue>(raw, version)
                        : _serializer.TryDeserialize<TValue>(raw).ThrowIfNull();
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to batch read {Type} count={Count}",
                typeof(TValue).Name, identities.Count);

            throw;
        }

        return result;
    }

    public async IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TValue : IStateValue, new()
    {
        var stateInfo = Registry.Get<TValue>();
        var cancellation = lifetime.Token;
        var latestVersion = _migrations.GetLatestVersion<TValue>();

        await using var connection = await _dbSource.Value.OpenConnectionAsync(cancellation);
        await using var command = connection.CreateCommand();

        var identity = new StateIdentity
        {
            Key = null!,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        command.CommandText = _cache.GetReadAllQuery(identity);
        command.Parameters.AddWithValue("type", stateInfo.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellation);

        while (await reader.ReadAsync(cancellation))
        {
            var key = ReadKeyFromReader<TKey>(reader, stateInfo);

            var payloadBinary = reader.GetFieldValue<byte[]>(1);
            var version = reader.GetFieldValue<int>(2);

            var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
            var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

            var value = version < latestVersion
                ? _migrations.Migrate<TValue>(raw, version)
                : _serializer.TryDeserialize<TValue>(raw)!;

            yield return (key, value);
        }
    }

    public async Task Write(StateWriteRequest request)
    {
        using var watch = MetricWatch.Start(BackendMetrics.StateWriteDuration);

        var records = request.Records;

        if (request.Transaction != null)
        {
            await WriteBatch(request.Transaction, records);
            BackendMetrics.StateWriteTotal.Add(1);
            BackendMetrics.StateWriteBatchSize.Record(records.Count);
            return;
        }

        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await WriteBatch(transaction, records);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to write {Count} records", records.Count);
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            BackendMetrics.StateWriteTotal.Add(1);
            BackendMetrics.StateWriteBatchSize.Record(records.Count);
        }
    }

    public async Task Delete(StateDeleteRequest request)
    {
        var identities = request.Identities;

        if (identities.Count == 0)
            return;

        using var watch = MetricWatch.Start(BackendMetrics.StateDeleteDuration);

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var groups = new Dictionary<(string TableName, bool HasExtension), List<StateIdentity>>();

            foreach (var identity in identities)
            {
                var key = (identity.TableName, identity.Extension != null);

                if (groups.TryGetValue(key, out var list) == false)
                {
                    list = new List<StateIdentity>();
                    groups[key] = list;
                }

                list.Add(identity);
            }

            foreach (var ((tableName, hasExtension), entries) in groups)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var conditions = new List<string>(entries.Count);

                for (var i = 0; i < entries.Count; i++)
                {
                    command.Parameters.AddWithValue($"key{i}", entries[i].Key);
                    command.Parameters.AddWithValue($"type{i}", entries[i].Type);

                    if (hasExtension)
                    {
                        command.Parameters.AddWithValue($"ext{i}", entries[i].Extension!);
                        conditions.Add($"(key = @key{i} AND type = @type{i} AND extension = @ext{i})");
                    }
                    else
                    {
                        conditions.Add($"(key = @key{i} AND type = @type{i})");
                    }
                }

                command.CommandText = $"DELETE FROM {tableName} WHERE {string.Join(" OR ", conditions)}";
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to delete {Count} records", identities.Count);
            throw;
        }
        finally
        {
            BackendMetrics.StateDeleteTotal.Add(1);
        }
    }

    private async Task<(string, int)> ReadRaw(StateIdentity stateIdentity)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = _cache.GetReadQuery(stateIdentity);

            command.Parameters.AddWithValue("type", stateIdentity.Type);
            command.Parameters.AddWithValue("key", stateIdentity.Key);

            if (stateIdentity.Extension != null)
                command.Parameters.AddWithValue("extension", stateIdentity.Extension);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                return (string.Empty, -1);

            var payloadBinary = reader.GetFieldValue<byte[]>(0);
            var version = reader.GetFieldValue<int>(1);

            if (payloadBinary == null || payloadBinary.Length == 0)
                throw new Exception();

            var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
            var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

            return (raw, version);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to read raw key={Key} type={StateType}",
                stateIdentity.Key, stateIdentity.Type);

            throw;
        }
    }

    public async Task<string> ReadRawJson(StateIdentity identity)
    {
        var (raw, _) = await ReadRaw(identity);
        return raw;
    }

    private async Task WriteBatch(
        NpgsqlTransaction transaction,
        IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        var groups =
            new Dictionary<(string TableName, bool HasExtension), List<(StateIdentity Identity, IStateValue Value)>>();

        foreach (var (identity, value) in records)
        {
            var key = (identity.TableName, identity.Extension != null);

            if (groups.TryGetValue(key, out var list) == false)
            {
                list = new List<(StateIdentity, IStateValue)>();
                groups[key] = list;
            }

            list.Add((identity, value));
        }

        foreach (var ((tableName, hasExtension), entries) in groups)
        {
            try
            {
                await using var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;

                var values = new List<string>(entries.Count);

                for (var i = 0; i < entries.Count; i++)
                {
                    var (identity, value) = entries[i];
                    var json = _serializer.Serialize(value);

                    command.Parameters.AddWithValue($"key{i}", identity.Key);
                    command.Parameters.AddWithValue($"type{i}", identity.Type);
                    command.Parameters.AddWithValue($"version{i}", value.Version);

                    var p = command.Parameters.AddWithValue($"value{i}", json);
                    p.NpgsqlDbType = NpgsqlDbType.Jsonb;

                    if (hasExtension)
                    {
                        command.Parameters.AddWithValue($"ext{i}", identity.Extension!);
                        values.Add($"(@key{i}, @type{i}, @version{i}, @value{i}::jsonb, @ext{i})");
                    }
                    else
                    {
                        values.Add($"(@key{i}, @type{i}, @version{i}, @value{i}::jsonb)");
                    }
                }

                var extensionCol = hasExtension ? ", extension" : "";
                var conflictCol = hasExtension ? ", extension" : "";

                command.CommandText = $@"
                    INSERT INTO {tableName} (key, type, version, value{extensionCol})
                    VALUES {string.Join(", ", values)}
                    ON CONFLICT (key, type{conflictCol})
                    DO UPDATE SET value = EXCLUDED.value
                ";

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[StateStorage] Failed to batch write {Count} records to {Table}",
                    entries.Count, tableName);

                throw;
            }
        }
    }

    private static TKey ReadKeyFromReader<TKey>(NpgsqlDataReader reader, GrainStateInfo info) => info.KeyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)reader.GetFieldValue<Guid>(0),
        GrainKeyType.String => (TKey)(object)reader.GetFieldValue<string>(0),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)reader.GetFieldValue<long>(0),
        _ => throw new InvalidOperationException($"[StateStorage] Unsupported key type: {info.KeyType}")
    };

    private static object BuildKeysArray(IList<StateIdentity> identities, GrainKeyType keyType) => keyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => identities.Select(i => (Guid)i.Key).ToArray(),
        GrainKeyType.String => identities.Select(i => (string)i.Key).ToArray(),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => identities.Select(i => (long)i.Key).ToArray(),
        _ => throw new InvalidOperationException($"[StateStorage] Unsupported key type: {keyType}")
    };
}