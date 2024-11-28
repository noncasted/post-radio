using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public class EventCollectionBase<T> : IEventCollection<T>, IEventResolver where T : class
    {
        private readonly List<T> _listeners = new();

        public Type Type => typeof(T);
        public IReadOnlyList<T> Listeners => _listeners;

        public void Add(T listener)
        {
            _listeners.Add(listener);
        }

        protected void Invoke(Action<T> callback)
        {
            foreach (var listener in _listeners)
                callback.Invoke(listener);
        }
    }
    
    public class AsyncEventCollectionBase<T> : IEventCollection<T>, IEventResolver where T : class
    {
        private readonly List<T> _listeners = new();

        public Type Type => typeof(T);
        public IReadOnlyList<T> Listeners => _listeners;

        public void Add(T listener)
        {
            _listeners.Add(listener);
        }

        protected async UniTask Invoke(Func<T, UniTask> callback)
        {
            foreach (var listener in _listeners)
                await callback.Invoke(listener);
        }
    }

    public class ScopeSetupCollection : EventCollectionBase<IScopeSetup>, IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime) => Invoke(l => l.OnSetup(lifetime));
    }

    public class ScopeSetupCompletionCollection : EventCollectionBase<IScopeSetupCompletion>, IScopeSetupCompletion
    {
        public void OnSetupCompletion(IReadOnlyLifetime lifetime) => Invoke(l => l.OnSetupCompletion(lifetime));
    }
}