using System.Diagnostics;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
    public Exception? Error { get; init; }

    public override string ToString() => Error != null
        ? $"TransactionResult(Success={IsSuccess}, Error={Error})"
        : $"TransactionResult(Success={IsSuccess})";
}

public class TransactionParameters
{
    public required Func<Task> Action { get; init; }
    public required List<Func<NpgsqlTransaction, Task>> Callbacks { get; init; }
}

public class TransactionCommitResult
{
    public required IReadOnlyList<GrainStateRecord> States { get; init; }
}

public interface ITransactions
{
    Task<TransactionResult> Process(TransactionParameters action);
}

public class Transactions : ITransactions
{
    public Transactions(
        IStateStorage stateStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<Transactions> logger)
    {
        _stateStorage = stateStorage;
        _dbSource = dbSource;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IDbSource _dbSource;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<Transactions> _logger;

    public async Task<TransactionResult> Process(TransactionParameters parameters)
    {
        using var activity = TraceExtensions.Transactions.StartActivity("Transaction.Process");
        using var watch = MetricWatch.Start(BackendMetrics.TransactionDuration);

        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        activity?.SetTag("transaction.id", context.Id);

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await parameters.Action();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] In action exception occured during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        TransactionCommitResult result;

        try
        {
            result = await CollectStates();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to collect commit result during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        activity?.SetTag("participant.count", context.Participants.Count);
        BackendMetrics.TransactionParticipantCount.Record(context.Participants.Count);

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                if (result.States.Count != 0)
                    await _stateStorage.Write(transaction, result.States);

                if (context.SideEffects.Count != 0)
                    await _sideEffectsStorage.Write(transaction, context.SideEffects.Values.ToList());

                foreach (var callback in parameters.Callbacks)
                    await callback(transaction);

                await transaction.CommitAsync();

                var confirmTasks = context.Participants.Select(t => t.Value.OnSuccess(context.Id));
                await Task.WhenAll(confirmTasks);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[Transaction] [Error] Failed to record changes during transaction {TransactionId}",
                    context.Id);

                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to commit to db during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        BackendMetrics.TransactionTotal.Add(1);
        BackendMetrics.TransactionSuccess.Add(1);

        return new TransactionResult
        {
            IsSuccess = true
        };

        async Task<TransactionCommitResult> CollectStates()
        {
            var states = new List<GrainStateRecord>();

            var collections = await Task.WhenAll(context.Participants.Select(p => Collect(p.Value)));

            foreach (var collection in collections)
                states.AddRange(collection.States);

            return new TransactionCommitResult()
            {
                States = states,
            };

            async Task<TransactionCommitResult> Collect(IGrainTransactionHandler handler)
            {
                var grainStates = new List<GrainStateRecord>();

                var result = await handler.CollectResult(context.Id);
                var participantId = handler.GetGrainId();

                foreach (var state in result.States)
                {
                    grainStates.Add(new GrainStateRecord
                    {
                        Id = participantId,
                        Value = state
                    });
                }

                return new TransactionCommitResult
                {
                    States = grainStates,
                };
            }
        }
    }

    private async Task Rollback(TransactionContext context, Activity? activity = null)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Transaction rolled back");
        BackendMetrics.TransactionRollback.Add(1);

        foreach (var (participantId, participant) in context.Participants)
        {
            try
            {
                await participant.OnFailure(context.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Transactions] Failed to rollback participant {ParticipantId}", participantId);
                BackendMetrics.TransactionRollbackFailure.Add(1);
            }
        }
    }
}