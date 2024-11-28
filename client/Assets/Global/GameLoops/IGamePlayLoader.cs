using Cysharp.Threading.Tasks;
using Internal;

namespace Global.GameLoops
{
    public interface IGamePlayLoader
    {
        UniTask Initialize(ILoadedScope parent);
    }
}