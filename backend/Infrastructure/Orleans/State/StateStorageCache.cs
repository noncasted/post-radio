using System.Collections.Concurrent;

namespace Infrastructure.State;

public class StateStorageCache
{
    private readonly ConcurrentDictionary<string, string> _readQueries = new();
    private readonly ConcurrentDictionary<string, string> _readExtQueries = new();
    private readonly ConcurrentDictionary<string, string> _readAllQueries = new();
    private readonly ConcurrentDictionary<string, string> _readBatchQueries = new();
    private readonly ConcurrentDictionary<string, string> _readBatchExtQueries = new();
    private readonly ConcurrentDictionary<string, string> _writeQueries = new();
    private readonly ConcurrentDictionary<string, string> _writeExtQueries = new();
    private readonly ConcurrentDictionary<string, string> _deleteQueries = new();
    private readonly ConcurrentDictionary<string, string> _deleteExtQueries = new();

    public string GetReadQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _readExtQueries : _readQueries;

        if (cache.TryGetValue(stateIdentity.Type, out var query))
            return query;

        var extensionClause = hasExtension ? "and extension = @extension" : string.Empty;

        query = $@"
            select value, version
            from {stateIdentity.TableName}
            where key = @key
            and type = @type
            {extensionClause}
            ";

        cache[stateIdentity.Type] = query;
        return query;
    }

    public string GetReadAllQuery(StateIdentity stateIdentity)
    {
        if (_readAllQueries.TryGetValue(stateIdentity.Type, out var query))
            return query;

        query = $"SELECT key, value, version FROM {stateIdentity.TableName} WHERE type = @type";

        _readAllQueries[stateIdentity.Type] = query;
        return query;
    }

    public string GetReadBatchQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _readBatchExtQueries : _readBatchQueries;

        if (cache.TryGetValue(stateIdentity.Type, out var query))
            return query;

        var extensionClause = hasExtension ? "AND extension = @extension" : string.Empty;

        query =
            $"SELECT key, value, version FROM {stateIdentity.TableName} WHERE type = @type AND key = ANY(@keys) {extensionClause}";

        cache[stateIdentity.Type] = query;
        return query;
    }

    public string GetWriteQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _writeExtQueries : _writeQueries;

        if (cache.TryGetValue(stateIdentity.Type, out var query))
            return query;

        var extension = hasExtension ? ", extension" : string.Empty;
        var extensionParam = hasExtension ? ", @extension" : string.Empty;

        query = $@"
            insert into {stateIdentity.TableName}
            (key, type, version, value{extension})
            values (@key, @type, @version, @value::jsonb{extensionParam})
            on conflict (key, type{extension})
            do update set value = EXCLUDED.value
            ";

        cache[stateIdentity.Type] = query;
        return query;
    }

    public string GetDeleteQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _deleteExtQueries : _deleteQueries;

        if (cache.TryGetValue(stateIdentity.Type, out var query))
            return query;

        var extensionClause = hasExtension ? " AND extension = @extension" : string.Empty;

        query = $"DELETE FROM {stateIdentity.TableName} WHERE key = @key AND type = @type{extensionClause}";

        cache[stateIdentity.Type] = query;
        return query;
    }
}