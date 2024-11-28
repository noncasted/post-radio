namespace Internal
{
    public interface ILifetime : IReadOnlyLifetime
    {
        void Terminate();
    }
}