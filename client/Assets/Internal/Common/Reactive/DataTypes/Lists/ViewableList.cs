using System;
using System.Collections.Generic;

namespace Internal
{
    public class ViewableList<TSource> : ViewableList<TSource, TSource>
    {
    }

    public class ViewableList<TSource, TView> : List<TSource>, IViewableList<TView> where TSource : TView
    {
        private readonly EventSource<IReadOnlyLifetime, TView> _eventSource = new();
        private readonly Dictionary<TView, ILifetime> _lifetimes = new();

        private bool _isDisposed;

        public new TView this[int index] => base[index];

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TView> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public IReadOnlyLifetime GetLifetime(TView value)
        {
            return _lifetimes[value];
        }

        public new IReadOnlyLifetime Add(TSource value)
        {
            if (_isDisposed == true)
                return new TerminatedLifetime();

            base.Add(value);
            var lifetime = new Lifetime();
            _lifetimes.Add(value, lifetime);

            _eventSource.Invoke(lifetime, value);

            OnModified();

            return lifetime;
        }

        public new void Remove(TSource value)
        {
            if (_isDisposed == true)
                return;

            base.Remove(value);
            _lifetimes[value].Terminate();

            OnModified();
        }

        public new void AddRange(IEnumerable<TSource> collection)
        {
            foreach (var value in collection)
                Add(value);
        }

        public new void RemoveRange(IEnumerable<TSource> collection)
        {
            foreach (var value in collection)
                Remove(value);
        }

        public new void RemoveAt(int index)
        {
            var source = this as IList<TSource>;
            var value = source[index];
            Remove(value);
        }

        public new IEnumerator<TView> GetEnumerator()
        {
            List<TSource> list = this;

            foreach (var source in list)
                yield return source;
        }

        public IEnumerable<TSource> GetEnumerable()
        {
            List<TSource> list = this;

            foreach (var source in list)
                yield return source;
        }

        public new void Clear()
        {
            foreach (var entry in this)
                _lifetimes[entry].Terminate();

            _lifetimes.Clear();

            OnModified();
        }

        protected virtual void OnModified()
        {
        }

        public void Dispose()
        {
            Clear();
            _eventSource.Dispose();
            _isDisposed = true;
        }
    }
}