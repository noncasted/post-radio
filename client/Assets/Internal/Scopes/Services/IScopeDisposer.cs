using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IScopeDisposer
    {
        UniTask Dispose();
    }
}