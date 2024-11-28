using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class AddressablesSceneLoadResult : ISceneLoadResult, ISceneInstanceProvider
    {
        public AddressablesSceneLoadResult(SceneInstance scene)
        {
            _scene = scene;
        }

        private readonly SceneInstance _scene;

        public Scene Scene => _scene.Scene;
        public SceneInstance SceneInstance => _scene;

        public bool TryGetSceneInstance(out SceneInstance scene)
        {
            scene = _scene;
            return false;
        }
    }
}