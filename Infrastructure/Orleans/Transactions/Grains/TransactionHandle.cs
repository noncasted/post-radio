using Orleans.Concurrency;
using Orleans.Placement;

namespace Infrastructure.Orleans;

[Reentrant]
[PreferLocalPlacement]
public class TransactionHandle : Grain, ITransactionHandle
{
    private readonly List<ITransactionHook> _hooks = new();

    public Task Warmup()
    {
        return Task.CompletedTask;
    }

    public Task AddListener(ITransactionHook hook)
    {
        _hooks.Add(hook);
        return Task.CompletedTask;
    }

    public Task OnSuccess(Guid transactionId)
    {
        return Task.WhenAll(_hooks.Select(t => t.OnSuccess(transactionId)));
    }

    public Task OnFailure(Guid transactionId)
    {
        return Task.WhenAll(_hooks.Select(t => t.OnFailure(transactionId)));
    }
}