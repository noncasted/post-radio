namespace Frontend
{
    public interface ILifetime : IReadOnlyLifetime
    {
        void Terminate();
    }
}