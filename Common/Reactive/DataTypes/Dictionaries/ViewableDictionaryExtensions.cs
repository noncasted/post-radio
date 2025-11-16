namespace Common;

public static class ViewableDictionaryExtensions
{
    public static void AddLifetimed<TKey, TSource, TView>(
        this ViewableDictionary<TKey, TSource, TView> dictionary,
        IReadOnlyLifetime lifetime,
        TKey key,
        TSource value)
        where TKey : notnull
        where TView : class
        where TSource : class, TView
    {
        dictionary.Add(key, value);
        lifetime.Listen(() => dictionary.Remove(key));
    }

    extension<TKey, TValue>(IViewableDictionary<TKey, TValue> dictionary)
    {
        public void Advise(
            IReadOnlyLifetime lifetime,
            Action<TKey, TValue> listener)
        {
            dictionary.Advise(lifetime, (_, key, value) => listener.Invoke(key, value));

            foreach (var (key, value) in dictionary)
                listener.Invoke(key, value);
        }

        public void View(
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, TKey, TValue> listener)
        {
            dictionary.Advise(lifetime, listener.Invoke);

            foreach (var (key, value) in dictionary)
                listener.Invoke(dictionary.GetLifetime(key), key, value);
        }

        public void View(
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, TValue> listener)
        {
            dictionary.Advise(lifetime, (valueLifetime, _, value) => listener.Invoke(valueLifetime, value));

            foreach (var (key, value) in dictionary)
                listener.Invoke(dictionary.GetLifetime(key), value);
        }

        public void View(
            IReadOnlyLifetime lifetime,
            Action<TValue> listener)
        {
            dictionary.Advise(lifetime, (_, _, value) => listener.Invoke(value));

            foreach (var (_, value) in dictionary)
                listener.Invoke(value);
        }
    }
}