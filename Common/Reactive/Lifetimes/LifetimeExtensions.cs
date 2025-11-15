namespace Common
{
    public static class LifetimeExtensions
    {
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
        
        public static IReadOnlyLifetime ToLifetime(this CancellationToken cancellation)
        {
            var child = new Lifetime();
            cancellation.Register(() => child.Terminate());
            return child;
        }
    }
}