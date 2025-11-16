using System.Text;

namespace Infrastructure.Orleans;

public static class DbGrainReaderExtensions
{
    public static Guid GetId(this DbGrainEntry entry)
    {
        var guidBytes = new byte[16];

        BitConverter.GetBytes(entry.Id0).CopyTo(guidBytes, 0);
        BitConverter.GetBytes(entry.Id1).CopyTo(guidBytes, 8);

        return new Guid(guidBytes);
    }

    public static DbGrainReader CreateDbReader(this IOrleans orleans, string table)
    {
        return new DbGrainReader(orleans, table);
    }

    public static T Deserialize<T>(this DbGrainReader reader, DbGrainEntry entry) where T : class
    {
        var stringPayload = Encoding.UTF8.GetString(entry.Payload);
        var value = reader.Orleans.Serializer.Deserialize(typeof(T), stringPayload) as T;
        return value!;
    }

    extension(DbGrainReader reader)
    {
        public DbGrainReader WhereType(string type)
        {
            reader.Where.Type = type;
            return reader;
        }

        public DbGrainReader WhereExtension(string extension)
        {
            reader.Where.Extension = extension;
            return reader;
        }

        public DbGrainReader SelectID()
        {
            reader.Select.Id = true;
            return reader;
        }

        public DbGrainReader SelectPayload()
        {
            reader.Select.Payload = true;
            return reader;
        }

        public DbGrainReader SelectExtension()
        {
            reader.Select.Extension = true;
            return reader;
        }
    }
}