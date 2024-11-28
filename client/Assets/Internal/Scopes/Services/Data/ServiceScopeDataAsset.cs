using UnityEngine;
using VContainer.Unity;

namespace Internal
{
    public class ServiceScopeDataAsset : EnvAsset
    {
        [SerializeField] private LifetimeScope _scopePrefab;
        [SerializeField] private SceneData _servicesScene;

        public ServiceScopeData Default => new(_scopePrefab, _servicesScene, false);
        public ServiceScopeData Mock => new(_scopePrefab, _servicesScene, true);
    }
}