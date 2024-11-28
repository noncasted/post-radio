using Cysharp.Threading.Tasks;

namespace Global.Systems
{
    public interface IUpdatableAction
    {
        UniTask Process();
    }
}