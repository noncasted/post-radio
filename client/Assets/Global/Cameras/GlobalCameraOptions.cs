using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public class GlobalCameraOptions : EnvAsset
    {
        [SerializeField] private GlobalCamera _prefab;

        public GlobalCamera Prefab => _prefab;
    }
}