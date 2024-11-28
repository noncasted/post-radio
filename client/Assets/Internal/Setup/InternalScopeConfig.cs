using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class InternalScopeConfig : EnvAsset, IInternalScopeConfig
    {
        [SerializeField] private PlatformType _platform;
        [SerializeField] private InternalScope _scope;
        [SerializeField] private AssetsStorage _assetsStorage;

        public PlatformType Platform => _platform;
        public InternalScope Scope => _scope;
        public IAssetsStorage AssetsStorage => _assetsStorage;

    }
}