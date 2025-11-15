using Common;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Transactions;

namespace Infrastructure.StorableActions;

public class BatchWriterTask<T> : IPriorityTask
{
    public required string Id { get; init; }
    public required TaskPriority Priority { get; init; }
    public required IBatchWriter<T> Batcher { get; init; }

    public Task Execute()
    {
        return Batcher.Loop();
    }
}

public class BatchWriterOptions
{
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
    public bool RequiresTransaction { get; set; } = true;
}

public abstract class BatchWriter<TState, TEntry> : CommonGrain, ITransactionHook, IBatchWriter<TEntry>
    where TState : BatchWriterState<TEntry>
{
    protected BatchWriter(IPersistentState<TState> state)
    {
        _state = state;
        _orleans = ServiceProvider.GetRequiredService<IOrleans>();
        _taskScheduler = ServiceProvider.GetRequiredService<ITaskScheduler>();
        _logger = ServiceProvider.GetRequiredService<ILogger<BatchWriter<TState, TEntry>>>();

        _task = new BatchWriterTask<TEntry>
        {
            Id = this.GetPrimaryKeyString(),
            Priority = TaskPriority.Low,
            Batcher = this.AsReference<IBatchWriter<TEntry>>()
        };
    }

    private readonly ILogger<BatchWriter<TState, TEntry>> _logger;
    private readonly IOrleans _orleans;
    private readonly ITaskScheduler _taskScheduler;

    private readonly IPersistentState<TState> _state;
    private readonly Dictionary<Guid, List<TEntry>> _pending = new();

    private readonly IPriorityTask _task;
    
    protected abstract BatchWriterOptions Options { get; }

    public Task Start()
    {
        if (_state.State.Entries.Count == 0)
            return Task.CompletedTask;

        _taskScheduler.Schedule(_task);
        return Task.CompletedTask;
    }

    public Task WriteTransactional(TEntry value)
    {
        this.AsTransactionHook();

        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        if (_pending.TryGetValue(transactionId, out var list) == false)
        {
            list = new List<TEntry>();
            _pending[transactionId] = list;
        }

        list.Add(value);
        return Task.CompletedTask;
    }
    
    public async Task WriteDirect(TEntry value)
    {
        _state.State.Entries.Add(value);
        await _state.WriteStateAsync();
        _taskScheduler.Schedule(_task);
    }

    public async Task OnSuccess(Guid transactionId)
    {
        _state.State.Entries.AddRange(_pending[transactionId]);
        _pending.Remove(transactionId);
        await _state.WriteStateAsync();
        _taskScheduler.Schedule(_task);
    }

    public Task OnFailure(Guid transactionId)
    {
        _pending.Remove(transactionId);
        return Task.CompletedTask;
    }

    public async Task Loop()
    {
        var state = _state.State;

        if (state.Entries.Count == 0)
            return;

        try
        {
            if (Options.RequiresTransaction == true)
            {
                await _orleans
                    .Transaction(() => Process(state.Entries))
                    .WithSuccessAction(() =>
                        {
                            state.Entries.Clear();
                            return _state.WriteStateAsync();
                        }
                    )
                    .Start();
            }
            else
            {
                await Process(state.Entries);
                state.Entries.Clear();
                await _state.WriteStateAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[BatchWriter] Process failed {writerName} {batchType}", 
                this.GetPrimaryKeyString(),
                typeof(TEntry).Name
            );
            
            _taskScheduler.Schedule(_task);
            return;
        }

        if (state.Entries.Count > 0)
            _taskScheduler.Schedule(_task);
    }

    protected abstract Task Process(IReadOnlyList<TEntry> entries);
}