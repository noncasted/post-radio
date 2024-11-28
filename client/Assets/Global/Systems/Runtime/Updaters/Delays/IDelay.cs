using Cysharp.Threading.Tasks;

namespace Global.Systems
{
    public interface IDelay
    {
        UniTask Run();
    }
}