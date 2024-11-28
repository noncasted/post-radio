using System;

namespace Internal
{
    public static class ViewableDictionaryExtensions
    {
        public static void Advise<TKey, TValue>(
            this IViewableDictionary<TKey, TValue> dictionary,
            IReadOnlyLifetime lifetime,
            Action<TKey, TValue> listener)
        {
            dictionary.Advise(lifetime, (_, key, value) => listener.Invoke(key, value));

            foreach (var (key, value) in dictionary)
                listener.Invoke(key, value);
        }

        public static void View<TKey, TValue>(
            this IViewableDictionary<TKey, TValue> dictionary,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, TKey, TValue> listener)
        {
            dictionary.Advise(lifetime, listener.Invoke);

            foreach (var (key, value) in dictionary)
                listener.Invoke(dictionary.GetLifetime(key), key, value);
        }

        public static void View<TKey, TValue>(
            this IViewableDictionary<TKey, TValue> dictionary,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, TValue> listener)
        {
            dictionary.Advise(lifetime, (valueLifetime, _, value) => listener.Invoke(valueLifetime, value));

            foreach (var (key, value) in dictionary)
                listener.Invoke(dictionary.GetLifetime(key), value);
        }

        public static void View<TKey, TValue>(
            this IViewableDictionary<TKey, TValue> dictionary,
            IReadOnlyLifetime lifetime,
            Action<TValue> listener)
        {
            dictionary.Advise(lifetime, (_, _, value) => listener.Invoke(value));

            foreach (var (_, value) in dictionary)
                listener.Invoke(value);
        }

        
        public static void AddLifetimed<TKey, TSource, TView>(
            this ViewableDictionary<TKey, TSource, TView> dictionary,
            IReadOnlyLifetime lifetime,
            TKey key, TSource value) where TSource : TView
        {
            dictionary.Add(key, value);
            lifetime.Listen(() => dictionary.Remove(key));
        }
    }
}