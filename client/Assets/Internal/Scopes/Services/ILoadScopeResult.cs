namespace Internal
{
    public interface ILoadScopeResult : ILoadedScope
    {
        IEventLoop EventLoop { get; }
    }
}