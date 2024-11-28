using UnityEngine.Events;

namespace Internal
{
    public static class LifetimeExtensions
    {
        public static void Listen<T>(this UnityEvent<T> source, IReadOnlyLifetime lifetime, UnityAction<T> listener)
        {
            source.AddListener(listener);

            lifetime.Listen(() => source.RemoveListener(listener));
        }

        public static ILifetime Child(this IReadOnlyLifetime lifetime)
        {
            var child = new Lifetime(lifetime);
            lifetime.Listen(child.Terminate);
            return child;
        }

        public static ILifetime Intersect(this IReadOnlyLifetime lifetimeA, IReadOnlyLifetime lifetimeB)
        {
            var child = new Lifetime();

            lifetimeA.Listen(OnTermination);
            lifetimeB.Listen(OnTermination);

            return child;

            void OnTermination()
            {
                child.Terminate();

                lifetimeA.RemoveListener(OnTermination);
                lifetimeB.RemoveListener(OnTermination);
            }
        }
    }
}