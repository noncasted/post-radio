using Cysharp.Threading.Tasks;

namespace Global.Systems
{
    public interface IProgression
    {
        UniTask Process();
    }
}