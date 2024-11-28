using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    public interface ISceneInstanceProvider
    {
        SceneInstance SceneInstance { get; }
    }
}