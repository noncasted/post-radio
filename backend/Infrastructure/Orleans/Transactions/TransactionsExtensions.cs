using Npgsql;

namespace Infrastructure;

public class TransactionBuilder
{
    public required TransactionParameters Parameters { get; init; }
    public required ITransactions Transactions { get; init; }
}

public static class TransactionsExtensions
{
    extension(ITransactions transactions)
    {
        public Task<TransactionResult> Run(Func<Task> action)
        {
            var parameters = new TransactionParameters
            {
                Action = action,
                Callbacks = []
            };

            return transactions.Process(parameters);
        }

        public async Task<T> Run<T>(Func<Task<T>> action)
        {
            T? result = default;

            var parameters = new TransactionParameters
            {
                Action = Process,
                Callbacks = []
            };

            await transactions.Process(parameters);

            if (result == null)
                throw new NullReferenceException("Result is null");

            return result;

            async Task Process()
            {
                result = await action();
            }
        }

        public TransactionBuilder CreateBuilder(Func<Task> action)
        {
            var builder = new TransactionBuilder
            {
                Parameters = new TransactionParameters
                {
                    Action = action,
                    Callbacks = new List<Func<NpgsqlTransaction, Task>>()
                },
                Transactions = transactions
            };

            return builder;
        }
    }

    extension(TransactionBuilder builder)
    {
        public TransactionBuilder WithCallback(Func<NpgsqlTransaction, Task> callback)
        {
            builder.Parameters.Callbacks.Add(callback);
            return builder;
        }

        public Task<TransactionResult> Run()
        {
            return builder.Transactions.Process(builder.Parameters);
        }
    }
}