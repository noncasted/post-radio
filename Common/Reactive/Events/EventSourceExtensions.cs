namespace Common
{
    public static class EventSourceExtensions
    {
        public static void Advise<T>(
            this IEventSource<T> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, _ => listener.Invoke());
        }
        
        public static void Advise<T1, T2>(
            this IEventSource<T1, T2> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, _) => listener.Invoke());
        }
        
        public static void Advise<T1, T2, T3>(
            this IEventSource<T1, T2, T3> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, _, _) => listener.Invoke());
        }
        
        public static void Advise<T1, T2>(
            this IEventSource<T1, T2> property,
            IReadOnlyLifetime lifetime,
            Action<T2> listener)
        {
            property.Advise(lifetime, (_, value2) => listener.Invoke(value2));
        }
    }
}