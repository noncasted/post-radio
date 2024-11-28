using System;
using System.Collections.Generic;

namespace Internal
{
    public class ViewableDictionary<TKey, TSource> : ViewableDictionary<TKey, TSource, TSource>
    {
    }

    public class ViewableDictionary<TKey, TSource, TView> :
        Dictionary<TKey, TSource>, IViewableDictionary<TKey, TView>
        where TSource : TView
    {
        private readonly EventSource<IReadOnlyLifetime, TKey, TView> _eventSource = new();
        private readonly Dictionary<TKey, ILifetime> _lifetimes = new();

        public new TView this[TKey key] => base[key];
        public new IEnumerable<TKey> Keys => base.Keys;

        public new IEnumerable<TView> Values
        {
            get
            {
                IEnumerable<TSource> values = base.Values;

                foreach (var value in values)
                    yield return value;
            }
        }

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TKey, TView> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public IReadOnlyLifetime GetLifetime(TKey value)
        {
            return _lifetimes[value];
        }

        public new IReadOnlyLifetime Add(TKey key, TSource value)
        {
            base.Add(key, value);
            var lifetime = new Lifetime();
            _lifetimes.Add(key, lifetime);

            _eventSource.Invoke(lifetime, key, value);

            OnModified();

            return lifetime;
        }

        public new void Remove(TKey key)
        {
            base.Remove(key);
            _lifetimes[key].Terminate();

            OnModified();
        }


        protected virtual void OnModified()
        {
        }

        public new IEnumerator<KeyValuePair<TKey, TView>> GetEnumerator()
        {
            IEnumerable<KeyValuePair<TKey, TSource>> enumerable = this;

            foreach (var pair in enumerable)
                yield return new KeyValuePair<TKey, TView>(pair.Key, pair.Value);
        }


        public new IEnumerable<KeyValuePair<TKey, TSource>> GetEnumerable()
        {
            IEnumerable<KeyValuePair<TKey, TSource>> enumerable = this;

            foreach (var pair in enumerable)
                yield return new KeyValuePair<TKey, TSource>(pair.Key, pair.Value);
        }

        public bool TryGetValue(TKey key, out TView value)
        {
            var result = base.TryGetValue(key, out var source);
            value = source;
            return result;
        }

        public bool TryGetSourceValue(TKey key, out TSource value)
        {
            return base.TryGetValue(key, out value);
        }

        public void Dispose()
        {
            Clear();
            _eventSource.Dispose();
        }
    }
}