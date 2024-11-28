using UnityEngine;

namespace Internal
{
    public abstract class SceneData : EnvAsset
    {
        [SerializeField] private SceneField _scene;

        public SceneField Scene => _scene;
    }
}