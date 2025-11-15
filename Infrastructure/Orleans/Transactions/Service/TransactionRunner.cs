using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Orleans.Serialization;
using Orleans.Transactions;

namespace Infrastructure.Orleans;

public class TransactionRunner : ITransactionRunner
{
    public TransactionRunner(
        ITransactionResolver transactionResolver,
        Serializer<OrleansTransactionAbortedException> serializer)
    {
        _transactionResolver = transactionResolver;
        _serializer = serializer;
    }

    private readonly ITransactionResolver _transactionResolver;
    private readonly Serializer<OrleansTransactionAbortedException> _serializer;

    public async Task Run(TransactionRunOptions options)
    {
        var transactionTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(10);
        var transactionInfo = await _transactionResolver.StartTransaction(readOnly: false, transactionTimeout);

        TransactionContextOverrides.SetTransactionInfo(transactionInfo);

        try
        {
            await options.Action();
        }
        catch (Exception exception)
        {
            transactionInfo.RecordException(exception, _serializer);
        }

        // Gather pending actions into transaction
        transactionInfo.ReconcilePending();

        // Check if transaction is pending for abort
        OrleansTransactionException transactionException = transactionInfo.MustAbort(_serializer);

        if (transactionException is not null || transactionInfo.TryToCommit is false)
        {
            // Transaction is pending for abort
            await _transactionResolver.Abort(transactionInfo);
        }
        else
        {
            // Try to resolve transaction
            var (status, exception) = await _transactionResolver.Resolve(transactionInfo, options);

            if (status != TransactionalStatus.Ok)
            {
                // Resolving transaction failed
                transactionException = status.ConvertToUserException(transactionInfo.Id, exception);
                ExceptionDispatchInfo.SetCurrentStackTrace(transactionException);
            }
        }

        if (transactionException != null)
        {
            // Transaction failed - bubble up exception
            ExceptionDispatchInfo.Throw(transactionException);
        }
    }
}