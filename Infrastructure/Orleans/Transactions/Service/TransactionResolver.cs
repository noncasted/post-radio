using Microsoft.Extensions.Logging;
using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public interface ITransactionResolver : ITransactionAgent
{
    Task<(TransactionalStatus, Exception?)> Resolve(TransactionInfo transactionInfo, TransactionRunOptions options);
}

public class TransactionResolver : ITransactionResolver
{
    private readonly ILogger _logger;
    private readonly CausalClock _clock;
    private readonly ITransactionAgentStatistics _statistics;
    private readonly ITransactionOverloadDetector _overloadDetector;
    private readonly ReadCommiter _readCommiter;
    private readonly WriteCommiter _writeCommiter;

    public TransactionResolver(
        IClock clock,
        ITransactionAgentStatistics statistics,
        ITransactionOverloadDetector overloadDetector,
        ILogger<TransactionResolver> logger)
    {
        _clock = new CausalClock(clock);
        _logger = logger;
        _statistics = statistics;
        _overloadDetector = overloadDetector;
        _readCommiter = new ReadCommiter(logger);
        _writeCommiter = new WriteCommiter(logger);
    }

    public Task<TransactionInfo> StartTransaction(bool readOnly, TimeSpan timeout)
    {
        if (_overloadDetector.IsOverloaded() == true)
        {
            _statistics.TrackTransactionThrottled();
            throw new OrleansStartTransactionFailedException(new OrleansTransactionOverloadException());
        }

        var guid = Guid.NewGuid();
        var ts = _clock.UtcNow();

        _statistics.TrackTransactionStarted();
        return Task.FromResult(new TransactionInfo(guid, ts, ts));
    }

    public Task<(TransactionalStatus, Exception?)> Resolve(TransactionInfo transactionInfo)
    {
        return Resolve(transactionInfo, TransactionRunOptions.Empty);
    }

    public async Task<(TransactionalStatus, Exception?)> Resolve(
        TransactionInfo transactionInfo,
        TransactionRunOptions options)
    {
        transactionInfo.TimeStamp = _clock.MergeUtcNow(transactionInfo.TimeStamp);

        if (transactionInfo.Participants.Count == 0)
        {
            _statistics.TrackTransactionSucceeded();
            return (TransactionalStatus.Ok, null);
        }

        var participants = new TransactionParticipants(transactionInfo);
        participants.Collect();

        try
        {
            var (status, exception) = participants.Write.Count switch
            {
                0 => await _readCommiter.Execute(participants),
                _ => await _writeCommiter.Execute(participants, options)
            };

            if (status == TransactionalStatus.Ok)
                _statistics.TrackTransactionSucceeded();
            else
                _statistics.TrackTransactionFailed();

            return (status, exception);
        }
        catch (Exception)
        {
            _statistics.TrackTransactionFailed();
            throw;
        }
    }

    public async Task Abort(TransactionInfo transactionInfo)
    {
        _statistics.TrackTransactionFailed();

        var participants = transactionInfo.Participants.Keys.ToList();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Abort {TransactionInfo} {Participants}", transactionInfo,
                string.Join(",", participants.Select(p => p.ToString()))
            );
        }

        // send one-way abort messages to release the locks and roll back any updates
        await Task.WhenAll(participants.Select(p =>
                {
                    var resourceExtension = p.AsResource();
                    return resourceExtension
                        .Abort(p.Name, transactionInfo.TransactionId);
                }
            )
        );
    }
}