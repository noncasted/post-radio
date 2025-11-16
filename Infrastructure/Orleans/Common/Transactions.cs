namespace Infrastructure.Orleans;

public interface ITransactions
{
    ITransactionClient Client { get; }
    ITransactionRunner Runner { get; }
}

public class Transactions : ITransactions
{
    public Transactions(ITransactionClient client, ITransactionRunner runner)
    {
        Client = client;
        Runner = runner;
    }

    public ITransactionClient Client { get; }
    public ITransactionRunner Runner { get; }
}

public static class TransactionsExtensions
{
    extension(ITransactions transactions)
    {
        public Task Create(Func<Task> action)
        {
            return transactions.Client.RunTransaction(TransactionOption.Create, action);
        }

        public Task Join(Func<Task> action)
        {
            return transactions.Client.RunTransaction(TransactionOption.Join, action);
        }

        public Task<T> Create<T>(Func<Task<T>> action)
        {
            return transactions.Run(TransactionOption.Create, action);
        }

        public Task<T> Join<T>(Func<Task<T>> action)
        {
            return transactions.Run(TransactionOption.Join, action);
        }

        public async Task<T> Run<T>(
            TransactionOption option,
            Func<Task<T>> action)
        {
            T result = default!;

            await transactions.Client.RunTransaction(option, async () => { result = await action(); });

            return result;
        }
    }
}