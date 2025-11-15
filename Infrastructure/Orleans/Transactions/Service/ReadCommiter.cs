using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public class ReadCommiter
{
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public ReadCommiter(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<(TransactionalStatus, Exception?)> Execute(TransactionParticipants participants)
    {
        Exception? exception;

        var status = TransactionalStatus.Ok;
        var info = participants.Info;

        try
        {
            var commitResults = await Commit();

            // examine the return status
            foreach (var commitStatus in commitResults)
            {
                if (commitStatus != TransactionalStatus.Ok)
                {
                    status = commitStatus;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "{TotalMilliseconds} fail {TransactionId} prepare response status={status}",
                            _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"), info.TransactionId,
                            status
                        );
                    }

                    break;
                }
            }

            exception = null;
        }
        catch (TimeoutException timeoutException)
        {
            HandleTimeoutException(timeoutException);
        }
        catch (Exception e)
        {
            HandleGenericException(e);
        }

        if (status != TransactionalStatus.Ok)
            await Cancel();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "{ElapsedMilliseconds} finish (reads only) {TransactionId}",
                info.TransactionId,
                _stopwatch.Elapsed.TotalMilliseconds.ToString("f2")
            );
        }

        return (status, exception);

        async Task<IReadOnlyList<TransactionalStatus>> Commit()
        {
            var tasks = new List<Task<TransactionalStatus>>();

            foreach (var resource in participants.Resources)
            {
                var resourceExtension = resource.Key.Reference.AsReference<ITransactionalResourceExtension>();

                tasks.Add(resourceExtension.CommitReadOnly(
                        resource.Key.Name,
                        info.TransactionId,
                        resource.Value,
                        info.TimeStamp
                    )
                );
            }

            // wait for all responses
            var results = await Task.WhenAll(tasks);
            return results;
        }

        void HandleTimeoutException(TimeoutException timeoutException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{TotalMilliseconds} timeout {TransactionId} on CommitReadOnly",
                    _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"), info.TransactionId
                );
            }

            status = TransactionalStatus.ParticipantResponseTimeout;
            exception = timeoutException;
        }

        void HandleGenericException(Exception e)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{TotalMilliseconds} failure {TransactionId} CommitReadOnly",
                    _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"), info.TransactionId
                );
            }

            _logger.LogWarning(e, "Unknown error while commiting readonly transaction {TransactionId}",
                info.TransactionId
            );

            status = TransactionalStatus.PresumedAbort;
            exception = e;
        }

        async Task Cancel()
        {
            try
            {
                await Task.WhenAll(participants.Resources.Select((resource) =>
                        {
                            var id = resource.Key;
                            var resourceExtension = id.Reference.AsReference<ITransactionalResourceExtension>();
                            return resourceExtension.Abort(id.Name, info.TransactionId);
                        }
                    )
                );
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        ex,
                        "{TotalMilliseconds} failure aborting {TransactionId} CommitReadOnly",
                        _stopwatch.Elapsed.TotalMilliseconds.ToString("f2"),
                        info.TransactionId
                    );
                }

                _logger.LogWarning(
                    ex,
                    "Failed to abort readonly transaction {TransactionId}",
                    info.TransactionId
                );
            }
        }
    }
}