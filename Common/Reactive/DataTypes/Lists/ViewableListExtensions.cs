namespace Common;

public static class ViewableListExtensions
{
    extension<T>(IViewableList<T> list)
    {
        public void View(IReadOnlyLifetime lifetime, Action<T> listener)
        {
            list.Advise(lifetime, (_, value) => listener.Invoke(value));

            foreach (var entry in list)
                listener.Invoke(entry);
        }

        public void View(
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener)
        {
            list.Advise(lifetime, listener.Invoke);

            foreach (var entry in list)
                listener.Invoke(list.GetLifetime(entry), entry);
        }
    }
}