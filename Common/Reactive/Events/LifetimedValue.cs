namespace Common;

public class LifetimedValue<T> : ILifetimedValue<T>
{
    public LifetimedValue(T value)
    {
        _lifetime = new Lifetime();
        Value = value;
    }

    private readonly EventSource<IReadOnlyLifetime, T> _eventSource = new();
    private Lifetime _lifetime;

    public T Value { get; private set; }

    public IReadOnlyLifetime ValueLifetime => _lifetime;

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
    {
        _eventSource.Advise(lifetime, handler);
    }

    public void Dispose()
    {
        _lifetime.Terminate();
        _eventSource.Dispose();
    }

    public void Set(T value)
    {
        _lifetime.Terminate();

        _lifetime = new Lifetime();
        Value = value;

        _eventSource.Invoke(_lifetime, value);
    }

    public void InvokeAdvices()
    {
        _eventSource.Invoke(_lifetime, Value);
    }
}