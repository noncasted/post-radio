using System;
using UnityEngine;

namespace Internal
{
    public class AssetEnvironment : IAssetEnvironment
    {
        public AssetEnvironment(
            IAssetsStorage assetsStorage,
            OptionsRegistry optionsRegistry)
        {
            _assetsStorage = assetsStorage;
            _optionsRegistry = optionsRegistry;
        }

        private readonly IAssetsStorage _assetsStorage;
        private readonly OptionsRegistry _optionsRegistry;
        
        public T GetAsset<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            return _assetsStorage.Assets[type.FullName] as T;
        }

        public T GetOptions<T>() where T : class, IOptionsEntry
        {
            if (_optionsRegistry.TryGetEntry<T>(out var options) == true)
                return options;

            throw new NullReferenceException();
        }
    }
}