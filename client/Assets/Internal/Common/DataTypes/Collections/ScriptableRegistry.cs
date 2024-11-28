using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    public interface IScriptableRegistry<T> where T : EnvAsset
    {
        IReadOnlyDictionary<int, T> Dictionary { get; }
        IReadOnlyList<T> Objects { get; }
    }

    public abstract class ScriptableRegistry<T> : EnvAsset, IScriptableRegistry<T>, IEnvAssetValidator
        where T : EnvAsset
    {
        [SerializeField] private T[] _objects;

        private IReadOnlyDictionary<int, T> _dictionary;

        public IReadOnlyList<T> Objects => _objects;

        public IReadOnlyDictionary<int, T> Dictionary
        {
            get
            {
                if (_dictionary == null || _dictionary.Count != _objects.Length)
                    _dictionary = _objects.ToDictionary(x => x.Id);

                return _dictionary;
            }
        }

        public void OnValidation()
        {
#if UNITY_EDITOR
            _objects = AssetsExtensions.FindAssets<T>();
#endif
            
            ProcessObjects();
        }
        
        protected virtual void ProcessObjects() {}
    }

    public static class ScriptableRegistryExtensions
    {
        public static T Get<T>(this IScriptableRegistry<T> registry, int id) where T : EnvAsset
        {
            return registry.Dictionary[id];
        }
    }
}