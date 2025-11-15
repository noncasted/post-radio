namespace Common
{
    public interface IReadOnlyLifetime
    {
        CancellationToken Token { get; }
        bool IsTerminated { get; }

        void Listen(Action callback);
        void RemoveListener(Action callback);
    }
}