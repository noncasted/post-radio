using Common.Extensions;

namespace Infrastructure.State;

public interface IStateValue
{
    int Version { get; }
}

public interface IGrainStateTransactionParticipant
{
    IStateValue GetState();
    void OnTransactionSuccess();
    void OnTransactionFailure();
}

public class State<T> : IGrainStateTransactionParticipant where T : class, IStateValue, new()
{
    public State(
        IStateStorage stateStorage,
        IGrainContext context,
        IStateSerializer serializer)
    {
        _stateStorage = stateStorage;
        _context = context;
        _serializer = serializer;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IGrainContext _context;
    private readonly IStateSerializer _serializer;

    private T? _value;
    private Guid _currentTransactionId;

    public T Value => _value.ThrowIfNull();

    public async Task Read()
    {
        if (TransactionContextProvider.Current == null)
        {
            if (_value != null)
                return;

            _value = await _stateStorage.Read<T>(_context.GrainId);
            return;
        }

        if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        if (_currentTransactionId == TransactionContextProvider.Current.Id)
            return;

        _currentTransactionId = TransactionContextProvider.Current.Id;
        _value = await _stateStorage.Read<T>(_context.GrainId);
        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);
    }

    public Task Write()
    {
        if (TransactionContextProvider.Current == null)
            return _stateStorage.Write(_context.GrainId, _value!);

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);

        return Task.CompletedTask;
    }

    public Task Replace(T value)
    {
        if (TransactionContextProvider.Current == null)
        {
            _value = value;
            return _stateStorage.Write(_context.GrainId, _value!);
        }

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        _value = value;
        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);

        return Task.CompletedTask;
    }

    public IStateValue GetState()
    {
        return _value.ThrowIfNull();
    }

    public void OnTransactionSuccess()
    {
        _currentTransactionId = Guid.Empty;
    }

    public void OnTransactionFailure()
    {
        _value = null;
        _currentTransactionId = Guid.Empty;
    }
}