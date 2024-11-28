using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IEventLoop
    {
        UniTask RunConstruct(IReadOnlyLifetime lifetime);
        UniTask RunLoaded(IReadOnlyLifetime lifetime);
        UniTask RunDispose();
    }
}