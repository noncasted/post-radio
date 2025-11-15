namespace Common
{
    public static class ViewableListExtensions
    {
        public static void View<T>(this IViewableList<T> list, IReadOnlyLifetime lifetime, Action<T> listener)
        {
            list.Advise(lifetime, (_, value) => listener.Invoke(value));

            foreach (var entry in list)
                listener.Invoke(entry);
        }

        public static void View<T>(
            this IViewableList<T> list,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener)
        {
            list.Advise(lifetime, listener.Invoke);

            foreach (var entry in list)
                listener.Invoke(list.GetLifetime(entry), entry);
        }
    }
}