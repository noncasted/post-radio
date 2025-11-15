namespace Infrastructure.Orleans;

public interface ITransactionHandle : IGrainWithGuidKey
{
    Task Warmup();
    Task AddListener(ITransactionHook hook);
    
    Task OnSuccess(Guid transactionId);
    Task OnFailure(Guid transactionId);
}