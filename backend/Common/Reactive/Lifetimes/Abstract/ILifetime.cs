namespace Common.Reactive
{
    public interface ILifetime : IReadOnlyLifetime
    {
        void Terminate();
    }
}