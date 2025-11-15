namespace Common
{
    public class LifetimedValue<T> : ILifetimedValue<T>
    {
        public LifetimedValue(T value)
        {
            _lifetime = new Lifetime();
            _value = value;
        }

        private readonly EventSource<IReadOnlyLifetime, T> _eventSource = new();

        private T _value;
        private Lifetime _lifetime;

        public T Value => _value;
        public IReadOnlyLifetime ValueLifetime => _lifetime;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public void Set(T value)
        {
            _lifetime.Terminate();

            _lifetime = new Lifetime();
            _value = value;

            _eventSource.Invoke(_lifetime, value);
        }

        public void InvokeAdvices()
        {
            _eventSource.Invoke(_lifetime, _value);
        }

        public void Dispose()
        {
            _lifetime.Terminate();
            _eventSource.Dispose();
        }
    }
}