using Cysharp.Threading.Tasks;

namespace Global.Systems
{
    public interface IResourcesCleaner
    {
        UniTask CleanUp();
    }
}