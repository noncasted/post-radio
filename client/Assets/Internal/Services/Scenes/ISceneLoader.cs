using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface ISceneLoader
    {
        UniTask<ISceneLoadResult> Load(SceneData data);
    }
}