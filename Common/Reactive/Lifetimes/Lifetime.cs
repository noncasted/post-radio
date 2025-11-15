namespace Common
{
    public class Lifetime : ILifetime
    {
        public Lifetime(IReadOnlyLifetime? parent = null)
        {
            _parent = parent;
        }

        private readonly IReadOnlyLifetime? _parent;
        private readonly ModifiableList<Action> _listeners = new();

        private CancellationTokenSource? _cancellation;
        private bool _isTerminated;

        public CancellationToken Token
        {
            get
            {
                _cancellation ??= new CancellationTokenSource();
                return _cancellation.Token;
            }
        }

        public bool IsTerminated => _isTerminated;

        public void Listen(Action callback)
        {
            if (_isTerminated == true)
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
            if (_isTerminated == true)
                return;

            _isTerminated = true;
            _cancellation?.Cancel();

            foreach (var listener in _listeners)
                listener.Invoke();

            _listeners.Clear();
            _parent?.RemoveListener(Terminate);
        }
    }
}