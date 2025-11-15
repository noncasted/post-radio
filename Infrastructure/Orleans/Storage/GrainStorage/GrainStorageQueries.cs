namespace Infrastructure.Orleans;

public class GrainStorageQueries
{
    public required string WriteToStorage { get; init; }
    public required string ReadFromStorage { get; init; }
    public required string ClearState { get; init; }

    public static GrainStorageQueries Create(string storageName)
    {
        var write = $"""
                     select * from {storageName}_writetostorage(@id_hash, @id_0, @id_1, @type_hash, @type, @extension, @version, @payload);
                     """;

        var read = $"""
                    SELECT
                        payload,
                        (now() at time zone 'utc'),
                        Version
                    FROM
                        {storageName}
                    WHERE
                        id_hash = @id_hash
                        AND type_hash = @type_hash AND @type_hash IS NOT NULL
                        AND id_0 = @id_0 AND @id_0 IS NOT NULL
                        AND id_1 = @id_1 AND @id_1 IS NOT NULL
                        AND type = @type AND type IS NOT NULL
                        AND ((@extension IS NOT NULL AND extension IS NOT NULL AND extension = @extension) OR @extension IS NULL AND extension IS NULL)
                    """;

        var clear = $"""
                     UPDATE {storageName}
                     SET
                         payload = NULL,
                         Version = Version + 1
                     WHERE
                         id_hash = @id_hash AND @id_hash IS NOT NULL
                         AND type_hash = @type_hash AND @type_hash IS NOT NULL
                         AND id_0 = @id_0 AND @id_0 IS NOT NULL
                         AND id_1 = @id_1 AND @id_1 IS NOT NULL
                         AND type = @type AND @type IS NOT NULL
                         AND ((@extension IS NOT NULL AND extension IS NOT NULL AND extension = @extension) OR @extension IS NULL AND extension IS NULL)
                         AND Version IS NOT NULL AND Version = @version AND @version IS NOT NULL
                     Returning Version as Newversion
                     """;

        return new GrainStorageQueries
        {
            WriteToStorage = write,
            ReadFromStorage = read,
            ClearState = clear
        };
    }
}