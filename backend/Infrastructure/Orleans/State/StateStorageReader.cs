using System.Text;
using Common;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

public class StatePageResult<TKey, TValue>
{
    public required IReadOnlyList<(TKey Key, TValue Value)> Entries { get; init; }
    public required int TotalCount { get; init; }
}

public interface IStateStorageReader
{
    Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(int offset, int limit)
        where TValue : IStateValue, new();
}

public class StateStorageReader : IStateStorageReader
{
    public StateStorageReader(
        IGrainStatesRegistry statesRegistry,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations,
        ILogger<StateStorageReader> logger)
    {
        _statesRegistry = statesRegistry;
        _dbSource = dbSource;
        _serializer = serializer;
        _migrations = migrations;
        _logger = logger;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly IStateMigrations _migrations;
    private readonly ILogger<StateStorageReader> _logger;

    public async Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(int offset, int limit)
        where TValue : IStateValue, new()
    {
        var stateInfo = _statesRegistry.Get<TValue>();
        var latestVersion = _migrations.GetLatestVersion<TValue>();

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            await using var countCmd = connection.CreateCommand();

            countCmd.CommandText =
                $"SELECT COUNT(*)::int FROM {stateInfo.TableName} WHERE type = @type AND value IS NOT NULL";
            countCmd.Parameters.AddWithValue("type", stateInfo.Name);
            var totalCount = (int)(await countCmd.ExecuteScalarAsync()).ThrowIfNull();

            await using var cmd = connection.CreateCommand();

            cmd.CommandText =
                $"SELECT key, value, version FROM {stateInfo.TableName} WHERE type = @type AND value IS NOT NULL ORDER BY key DESC OFFSET @offset LIMIT @limit";
            cmd.Parameters.AddWithValue("type", stateInfo.Name);
            cmd.Parameters.AddWithValue("offset", offset);
            cmd.Parameters.AddWithValue("limit", limit);

            var entries = new List<(TKey Key, TValue Value)>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                try
                {
                    var key = ReadKeyFromReader<TKey>(reader, stateInfo);

                    var payloadBinary = reader.GetFieldValue<byte[]>(1);
                    var version = reader.GetFieldValue<int>(2);

                    var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
                    var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

                    var value = version < latestVersion
                        ? _migrations.Migrate<TValue>(raw, version)
                        : _serializer.TryDeserialize<TValue>(raw)!;

                    entries.Add((key, value));
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[StateStorageReader] Failed to deserialize entry, skipping");
                }
            }

            return new StatePageResult<TKey, TValue>
            {
                Entries = entries,
                TotalCount = totalCount
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorageReader] Failed to read page {Type} (offset={Offset}, limit={Limit})",
                typeof(TValue).Name, offset, limit);

            return new StatePageResult<TKey, TValue>
            {
                Entries = [],
                TotalCount = 0
            };
        }
    }

    private static TKey ReadKeyFromReader<TKey>(NpgsqlDataReader reader, GrainStateInfo info) => info.KeyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)reader.GetFieldValue<Guid>(0),
        GrainKeyType.String => (TKey)(object)reader.GetFieldValue<string>(0),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)reader.GetFieldValue<long>(0),
        _ => throw new InvalidOperationException($"[StateStorageReader] Unsupported key type: {info.KeyType}")
    };
}