namespace Common;

public static class LifetimeExtensions
{
    public static IReadOnlyLifetime ToLifetime(this CancellationToken cancellation)
    {
        var child = new Lifetime();
        cancellation.Register(() => child.Terminate());
        return child;
    }

    extension(IReadOnlyLifetime lifetime)
    {
        public ILifetime Child()
        {
            var child = new Lifetime(lifetime);
            lifetime.Listen(child.Terminate);
            return child;
        }

        public ILifetime Intersect(IReadOnlyLifetime lifetimeB)
        {
            var child = new Lifetime();

            lifetime.Listen(OnTermination);
            lifetimeB.Listen(OnTermination);

            return child;

            void OnTermination()
            {
                child.Terminate();

                lifetime.RemoveListener(OnTermination);
                lifetimeB.RemoveListener(OnTermination);
            }
        }
    }
}