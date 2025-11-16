namespace Common;

public class Lifetime : ILifetime
{
    public Lifetime(IReadOnlyLifetime? parent = null)
    {
        _parent = parent;
    }

    private readonly ModifiableList<Action> _listeners = new();

    private readonly IReadOnlyLifetime? _parent;

    private CancellationTokenSource? _cancellation;

    public CancellationToken Token
    {
        get
        {
            _cancellation ??= new CancellationTokenSource();
            return _cancellation.Token;
        }
    }

    public bool IsTerminated { get; private set; }

    public void Listen(Action callback)
    {
        if (IsTerminated == true)
        {
            callback.Invoke();
            return;
        }

        _listeners.Add(callback);
    }

    public void RemoveListener(Action callback)
    {
        _listeners.Remove(callback);
    }

    public void Terminate()
    {
        if (IsTerminated == true)
            return;

        IsTerminated = true;
        _cancellation?.Cancel();

        foreach (var listener in _listeners)
            listener.Invoke();

        _listeners.Clear();
        _parent?.RemoveListener(Terminate);
    }
}