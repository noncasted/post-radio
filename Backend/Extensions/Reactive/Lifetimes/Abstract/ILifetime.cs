namespace Extensions
{
    public interface ILifetime : IReadOnlyLifetime
    {
        void Terminate();
    }
}