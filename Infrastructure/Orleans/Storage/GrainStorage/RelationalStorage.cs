using System.Data;

namespace Infrastructure.Orleans;

public interface IRelationalStorage
{
    /// <summary>
    /// Executes a given statement. Especially intended to use with <em>SELECT</em> statement.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="parameterProvider">Adds parameters to the query. The parameters must be in the same order with same names as defined in the query.</param>
    /// <param name="selector">This function transforms the raw <see cref="IDataRecord"/> results to type <see paramref="TResult"/> the <see cref="int"/> parameter being the resultset number.</param>
    /// <returns>GrainStorageReadResult of the <see paramref="query"/>.</returns>
    Task<GrainStorageReadResult<TResult>> Read<TResult>(
        string query,
        Action<IDbCommand> parameterProvider,
        Func<IDataRecord, TResult> selector);
}

public class RelationalStorage : IRelationalStorage
{
    private readonly string _connectionString;
    private readonly string _name;

    private RelationalStorage(string name, string connectionString)
    {
        _connectionString = connectionString;
        _name = name;
    }

    public static IRelationalStorage Create(string invariantName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(invariantName))
            throw new ArgumentException("The name of invariant must contain characters", nameof(invariantName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must contain characters", nameof(connectionString));

        return new RelationalStorage(invariantName, connectionString);
    }

    public async Task<GrainStorageReadResult<TResult>> Read<TResult>(
        string query,
        Action<IDbCommand> parameterProvider,
        Func<IDataRecord, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        await using var connection = DbConnectionFactory.CreateConnection(_name, _connectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        await using var command = connection.CreateCommand();
        parameterProvider.Invoke(command);
        command.CommandText = query;

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(continueOnCapturedContext: false);

        if (await reader.ReadAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false))
        {
            return new GrainStorageReadResult<TResult>
            {
                IsSuccess = true,
                Value = selector(reader)
            };
        }

        return new GrainStorageReadResult<TResult>
        {
            IsSuccess = false,
            Value = default(TResult)
        };
    }
}

public class GrainStorageReadResult<TResult>
{
    public required bool IsSuccess { get; init; }
    public required TResult Value { get; init; }
}