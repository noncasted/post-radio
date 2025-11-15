using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Common;

public interface IDbSource
{
    NpgsqlDataSource Value { get; }
}

public class DbSource : IDbSource
{
    public DbSource(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionNames.Postgres)!;
        Value = NpgsqlDataSource.Create(connectionString);
    }
    
    public NpgsqlDataSource Value { get; }
}

public static class DbSourceExtensions
{
    public static ValueTask<NpgsqlConnection> OpenConnection(this IDbSource dbSource)
    {
        return dbSource.Value.OpenConnectionAsync(CancellationToken.None);
    }
}