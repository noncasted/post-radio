using UnityEngine.SceneManagement;

namespace Internal
{
    public class NativeSceneLoadResult : ISceneLoadResult
    {
        public NativeSceneLoadResult(Scene scene)
        {
            _scene = scene;
        }
        
        private readonly Scene _scene;

        public Scene Scene => _scene;
    }
}