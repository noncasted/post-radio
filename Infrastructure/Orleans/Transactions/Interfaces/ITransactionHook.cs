using Common;
using Orleans.Transactions;

namespace Infrastructure.Orleans;

public interface ITransactionHook
{
    Task OnSuccess(Guid transactionId);
    Task OnFailure(Guid transactionId);
}

public static class TransactionHookExtensions
{
    public static Task AddTransactionHook(this IGrainFactory grains, ITransactionHook hook)
    {
        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        var handle = grains.GetGrain<ITransactionHandle>(transactionId);
        return handle.AddListener(hook);
    }
    
    public static Task AsTransactionHook<T>(this T grain) where T : ITransactionHook, ICommonGrain
    {
        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        var handle = grain.Grains.GetGrain<ITransactionHandle>(transactionId);
        return handle.AddListener(grain);
    }
}