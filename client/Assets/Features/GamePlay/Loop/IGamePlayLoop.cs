using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Loop
{
    public interface IGamePlayLoop
    {
        UniTask Process(IReadOnlyLifetime lifetime);
    }
}