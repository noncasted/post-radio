namespace Common
{
    public interface ILifetime : IReadOnlyLifetime
    {
        void Terminate();
    }
}