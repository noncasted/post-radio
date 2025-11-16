using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public class WriteCommiter
{
    public WriteCommiter(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public async Task<(TransactionalStatus, Exception?)> Execute(
        TransactionParticipants participants,
        TransactionRunOptions options)
    {
        TransactionalStatus status;
        Exception? exception;

        var info = participants.Info;
        var manager = participants.Manager;

        if (manager.Key.Reference == null)
            throw new Exception("Transaction manager reference is null");

        try
        {
            Prepare();

            // wait for the TM to commit the transaction
            var managerExtension = manager.Key.Reference.AsReference<ITransactionManagerExtension>();

            status = await managerExtension.PrepareAndCommit(
                manager.Key.Name,
                info.TransactionId,
                manager.Value,
                info.TimeStamp,
                participants.Write,
                participants.Resources.Count
            );

            if (options.SuccessAction != null)
                await options.SuccessAction();

            exception = null;
        }
        catch (TimeoutException ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug(
                    "{TotalMilliseconds} timeout {TransactionId} on CommitReadWriteTransaction",
                    _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                    info.TransactionId
                );

            status = TransactionalStatus.TMResponseTimeout;
            exception = ex;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug(
                    "{TotalMilliseconds} failure {TransactionId} CommitReadWriteTransaction",
                    _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                    info.TransactionId
                );

            _logger.LogWarning(
                ex,
                "Unknown error while committing transaction {TransactionId}",
                info.TransactionId
            );

            status = TransactionalStatus.PresumedAbort;
            exception = ex;
        }

        if (status != TransactionalStatus.Ok)
            await Cancel();

        if (_logger.IsEnabled(LogLevel.Trace) == true)
            _logger.LogTrace(
                "{TotalMilliseconds} finish {TransactionId}",
                _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                info.TransactionId
            );

        return (status, exception);

        void Prepare()
        {
            foreach (var (id, counter) in participants.Resources)
            {
                if (id.Equals(manager.Key) == true)
                    continue;

                // one-way prepare message
                var resourceExtension = id.Reference.AsReference<ITransactionalResourceExtension>();
                resourceExtension.Prepare(id.Name, info.TransactionId, counter, info.TimeStamp, manager.Key).Ignore();
            }
        }

        async Task Cancel()
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug) == true)
                    _logger.LogDebug(
                        "{TotalMilliseconds} failed {TransactionId} with status={Status}",
                        _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                        info.TransactionId,
                        status
                    );

                // notify participants
                if (status.DefinitelyAborted() == true)
                    await Task.WhenAll(
                        participants.Write
                            .Where(p => p.Equals(manager.Key) == false)
                            .Select(p => p.AsResource().Cancel(p.Name, info.TransactionId, info.TimeStamp, status)
                            )
                    );

                if (options.FailureAction != null)
                    await options.FailureAction();
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug(
                        "{TotalMilliseconds} failure aborting {TransactionId} CommitReadWriteTransaction",
                        _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                        info.TransactionId
                    );

                _logger.LogWarning(
                    ex,
                    "Failed to abort transaction {TransactionId}",
                    info.TransactionId
                );
            }
        }
    }
}