using Common.Reactive;
using Infrastructure;

namespace Tests.Fixtures;

/// <summary>
/// Simple mock implementation of IAddressableState for tests.
/// Pre-initialized with default values, no messaging or DB needed.
/// </summary>
public class TestAddressableState<T> : ViewableProperty<T>, IAddressableState<T>
    where T : class, new()
{
    public TestAddressableState() : base(new T())
    {
    }

    public TestAddressableState(T value) : base(value)
    {
    }

    public bool IsInitialized => true;

    public Task SetValue(T value)
    {
        Set(value);
        return Task.CompletedTask;
    }
}