using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    public abstract class EnvAsset : ScriptableObject
    {
        [ReadOnly] [SerializeField] private int _assetId = -1;

        public int Id => _assetId;

        public void SetId(int id)
        {
            _assetId = id;
            
            OnReload();
        }
        
        protected virtual void OnReload() {}
    }
}