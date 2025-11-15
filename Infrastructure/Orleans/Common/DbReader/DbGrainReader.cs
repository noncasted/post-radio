using Common;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Orleans;

public class DbGrainReader
{
    private readonly string _table;

    public readonly DbGrainReaderWhere Where = new();
    public readonly DbGrainReaderSelect Select = new();

    public IOrleans Orleans { get; }

    public DbGrainReader(IOrleans orleans, string table)
    {
        Orleans = orleans;
        _table = table;
    }

    public async Task<int> Count(CancellationToken cancellation = default)
    {
        await using var connection = await Orleans.DbSource.OpenConnection();
        await using var command = connection.CreateCommand();

        var query = $"SELECT COUNT(*) FROM {_table}";

        var where = Where.FormQuery();

        if (where != string.Empty)
            query += $" WHERE {where}";

        command.CommandText = query;
        Where.FillParameters(command);

        var result = await command.ExecuteScalarAsync(cancellation);

        if (result is long count)
            return (int)count;

        return 0;
    }

    public async IAsyncEnumerable<DbGrainEntry> Read(CancellationToken cancellation = default)
    {
        await using var connection = await Orleans.DbSource.OpenConnection();
        await using var command = connection.CreateCommand();

        Select.Validate();
        var select = Select.FormQuery();
        var query = $"SELECT {select} FROM {_table}";

        var where = Where.FormQuery();

        if (where != string.Empty)
            query += $" WHERE {where}";

        command.CommandText = query;
        Where.FillParameters(command);

        await using var reader = await command.ExecuteReaderAsync(cancellation);

        while (await reader.ReadAsync(cancellation))
        {
            var entry = new DbGrainEntry();

            try
            {
                if (Select.Id == true)
                {
                    if (reader["id_0"] is not long id0 || reader["id_1"] is not long id1)
                        throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                    entry.Id0 = id0;
                    entry.Id1 = id1;
                }

                if (Select.Payload == true)
                {
                    if (reader["payload"] is not byte[] payloadBytes)
                        throw new InvalidOperationException("Payload binary field is not present or invalid.");

                    entry.Payload = payloadBytes;
                }

                if (Select.Extension == true)
                {
                    if (reader["extension"] is not string extension)
                        throw new InvalidOperationException("Grain ID extension field is not present or invalid.");

                    entry.Extension = extension;
                }
            }
            catch (Exception e)
            {
                Orleans.Logger.LogError(e, "Error reading grain entry.");
            }

            yield return entry;
        }
    }
}

public class DbGrainEntry
{
    public long Id0 { get; set; }
    public long Id1 { get; set; }
    public string Extension { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = [];
}