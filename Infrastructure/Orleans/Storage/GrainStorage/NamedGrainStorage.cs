using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using Orleans.Serialization.Serializers;
using Orleans.Storage;

namespace Infrastructure.Orleans;

public static class NamedGrainStorageFactory
{
    public static NamedGrainStorage Create(IServiceProvider services, string name, string connectionString)
    {
        return new NamedGrainStorage(
            services.GetRequiredService<IActivatorProvider>(),
            services.GetRequiredService<ILogger<NamedGrainStorage>>(),
            name,
            connectionString
        );
    }
}

public class NamedGrainStorage : IGrainStorage
{
    private readonly IActivatorProvider _activatorProvider;
    private readonly IHasher _hasher;
    private readonly IGrainStorageSerializer _serializer;
    private readonly GrainTypeExtractor _typeExtractor;

    private readonly string _name;

    private readonly ILogger _logger;

    private readonly GrainStorageQueries _queries;
    private readonly IRelationalStorage _storage;

    public NamedGrainStorage(
        IActivatorProvider activatorProvider,
        ILogger<NamedGrainStorage> logger,
        string name,
        string connectionString)
    {
        _name = name;
        _activatorProvider = activatorProvider;
        _logger = logger;
        _hasher = new OrleansDefaultHasher();
        _typeExtractor = new GrainTypeExtractor();

        _queries = GrainStorageQueries.Create(name);
        _storage = RelationalStorage.Create(name, connectionString);

        var serializerOptions = Options.Create(new OrleansJsonSerializerOptions());
        var orleansSerializer = new OrleansJsonSerializer(serializerOptions);
        _serializer = new JsonGrainStorageSerializer(orleansSerializer);

        _logger.LogInfoInitializedStorageProvider(
            name,
            name,
            new GrainStorageLogs.ConnectionStringLogRecord(connectionString)
        );
    }

    /// <summary>Clear state data function for this storage provider.</summary>
    /// <see cref="IGrainStorage.ClearStateAsync{T}"/>.
    public async Task ClearStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var grainKey = grainId.ToKey();
        var baseGrainType = _typeExtractor.Extract(grainType);

        _logger.LogTraceClearingGrainState(_name, baseGrainType, grainKey, grainState.ETag);

        if (grainState.RecordExists == false)
        {
            await ReadStateAsync(grainType, grainId, grainState).ConfigureAwait(false);

            if (grainState.RecordExists == false)
                return;
        }

        try
        {
            var grainIdHash = _hasher.Hash(grainKey.GetHashBytes());
            var grainTypeHash = _hasher.Hash(Encoding.UTF8.GetBytes(baseGrainType));
            var query = _queries.ClearState;

            var result = await _storage.Read(query, PassParameters, Select).ConfigureAwait(false);

            var storageVersion = result.IsSuccess ? result.Value : string.Empty;

            GrainStorageVersionGuard.Check(
                "ClearState",
                _name,
                storageVersion,
                grainState.ETag,
                baseGrainType,
                grainKey.ToString()
            );

            grainState.ETag = storageVersion;
            grainState.RecordExists = false;
            grainState.State = CreateInstance<T>();

            _logger.LogTraceClearedGrainState(_name, baseGrainType, grainKey, grainState.ETag);

            void PassParameters(IDbCommand command)
            {
                command.AddParameter(StorageColumns.IdHash, grainIdHash);
                command.AddParameter(StorageColumns.Id_0, grainKey.Id_0);
                command.AddParameter(StorageColumns.Id_1, grainKey.Id_1);
                command.AddParameter(StorageColumns.TypeHash, grainTypeHash);
                command.AddParameter(StorageColumns.Type, baseGrainType);
                command.AddParameter(StorageColumns.Extension, grainKey.StringKey);
                command.AddParameter(StorageColumns.Version, ParseETag(grainState));
            }

            string Select(IDataRecord record)
            {
                return record.GetValue(0).ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorClearingGrainState(ex, _name, baseGrainType, grainKey, grainState.ETag);
            throw;
        }
    }

    /// <summary> Read state data function for this storage provider.</summary>
    /// <see cref="IGrainStorage.ReadStateAsync{T}"/>.
    public async Task ReadStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var grainKey = grainId.ToKey();
        var baseGrainType = _typeExtractor.Extract(grainType);
        
        _logger.LogTraceReadingGrainState(_name, baseGrainType, grainKey, grainState.ETag);

        try
        {
            var grainIdHash = _hasher.Hash(grainKey.GetHashBytes());
            var grainTypeHash = _hasher.Hash(Encoding.UTF8.GetBytes(baseGrainType));
            var query = _queries.ReadFromStorage;

            var result = await _storage.Read(query, PassParameters, Select).ConfigureAwait(false);

            if (result.IsSuccess == false)
            {
                grainState.State = CreateInstance<T>();
                grainState.ETag = string.Empty;
                grainState.RecordExists = false;
            }
            else
            {
                grainState.State = result.Value.State;
                grainState.ETag = result.Value.Version;
                grainState.RecordExists = true;
            }

            _logger.LogTraceReadGrainState(_name, baseGrainType, grainKey, grainState.ETag);

            void PassParameters(IDbCommand command)
            {
                command.AddParameter(StorageColumns.IdHash, grainIdHash);
                command.AddParameter(StorageColumns.Id_0, grainKey.Id_0);
                command.AddParameter(StorageColumns.Id_1, grainKey.Id_1);
                command.AddParameter(StorageColumns.TypeHash, grainTypeHash);
                command.AddParameter(StorageColumns.Type, baseGrainType);
                command.AddParameter(StorageColumns.Extension, grainKey.StringKey);
            }

            PayloadData<T> Select(IDataRecord record)
            {
                T storageState;
                var payload = record.GetValueOrDefault<byte[]?>(StorageColumns.Payload);

                if (payload != null)
                {
                    var data = new BinaryData(payload);
                    storageState = _serializer.Deserialize<T>(data)!;
                }
                else
                {
                    _logger.LogTraceNullGrainStateRead(_name, baseGrainType, grainKey, grainState.ETag);
                    storageState = CreateInstance<T>();
                }

                var version = record.GetNullableInt32("Version");

                return new PayloadData<T>
                {
                    State = storageState,
                    Version = version?.ToString(CultureInfo.InvariantCulture)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorReadingGrainState(ex, _name, baseGrainType, grainKey, grainState.ETag);
            throw;
        }
    }

    /// <summary> Write state data function for this storage provider.</summary>
    /// <see cref="IGrainStorage.WriteStateAsync{T}"/>
    public async Task WriteStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var grainKey = grainId.ToKey();
        var baseGrainType = _typeExtractor.Extract(grainType);

        _logger.LogTraceWritingGrainState(_name, baseGrainType, grainKey, grainState.ETag);

        try
        {
            var grainIdHash = _hasher.Hash(grainKey.GetHashBytes());
            var grainTypeHash = _hasher.Hash(Encoding.UTF8.GetBytes(baseGrainType));
            var query = _queries.WriteToStorage;

            var result = await _storage.Read(query, PassParameters, Select).ConfigureAwait(false);
            var storageVersion = result.IsSuccess ? result.Value : string.Empty;

            GrainStorageVersionGuard.Check(
                "WriteState",
                _name,
                storageVersion,
                grainState.ETag,
                baseGrainType,
                grainKey.ToString()
            );

            grainState.ETag = storageVersion;
            grainState.RecordExists = true;

            _logger.LogTraceWroteGrainState(_name, baseGrainType, grainKey, grainState.ETag);

            void PassParameters(IDbCommand command)
            {
                var serialized = _serializer.Serialize(grainState.State);

                command.AddParameter(StorageColumns.IdHash, grainIdHash);
                command.AddParameter(StorageColumns.Id_0, grainKey.Id_0);
                command.AddParameter(StorageColumns.Id_1, grainKey.Id_1);
                command.AddParameter(StorageColumns.TypeHash, grainTypeHash);
                command.AddParameter(StorageColumns.Type, baseGrainType);
                command.AddParameter(StorageColumns.Extension, grainKey.StringKey);
                command.AddParameter(StorageColumns.Version, ParseETag(grainState));
                command.AddParameter(StorageColumns.Payload, serialized.ToArray());
            }

            string Select(IDataRecord record)
            {
                return record.GetNullableInt32("NewGrainStateVersion").ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorWritingGrainState(ex, _name, baseGrainType, grainKey, grainState.ETag);
            throw;
        }
    }

    private T CreateInstance<T>() => _activatorProvider.GetActivator<T>().Create();

    private int? ParseETag<T>(IGrainState<T> state)
    {
        if (string.IsNullOrWhiteSpace(state.ETag) == false)
            return int.Parse(state.ETag, CultureInfo.InvariantCulture);

        return null;
    }

    public class PayloadData<T>
    {
        public required T State { get; init; }
        public required string? Version { get; init; }
    }
}