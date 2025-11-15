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
    public static Task Create(this ITransactions transactions, Func<Task> action)
    {
        return transactions.Client.RunTransaction(TransactionOption.Create, action);
    }

    public static Task Join(this ITransactions transactions, Func<Task> action)
    {
        return transactions.Client.RunTransaction(TransactionOption.Join, action);
    }

    public static Task<T> Create<T>(this ITransactions transactions, Func<Task<T>> action)
    {
        return transactions.Run(TransactionOption.Create, action);
    }

    public static Task<T> Join<T>(this ITransactions transactions, Func<Task<T>> action)
    {
        return transactions.Run(TransactionOption.Join, action);
    }

    public static async Task<T> Run<T>(
        this ITransactions transactions,
        TransactionOption option,
        Func<Task<T>> action)
    {
        T result = default!;

        await transactions.Client.RunTransaction(option, async () => { result = await action(); });

        return result;
    }
}