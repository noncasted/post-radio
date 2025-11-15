using Npgsql;

namespace Infrastructure.Orleans;

public class DbGrainReaderWhere
{
    public string Type { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;

    public string FormQuery()
    {
        var entries = new List<string>();

        if (Type != string.Empty)
            entries.Add("type = @type");

        if (Extension != string.Empty)
            entries.Add("extension = @extension");

        return entries.Count > 0 ? string.Join(" AND ", entries) : string.Empty;
    }

    public void FillParameters(NpgsqlCommand command)
    {
        if (Type != string.Empty)
            command.Parameters.AddWithValue("@type", Type);

        if (Extension != string.Empty)
            command.Parameters.AddWithValue("@extension", Extension);
    }
}