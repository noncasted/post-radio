using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    public class AssetsExtensions
    {
        public static T[] FindAssets<T>() where T : Object
        {
#if UNITY_EDITOR

            var objects = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T)}");

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset == null)
                    continue;

                objects.Add(asset);
            }

            return objects.ToArray();
#endif
            return Array.Empty<T>();
        }

        public static T FindAsset<T>() where T : Object
        {
#if UNITY_EDITOR

            var objects = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T)}");

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset == null)
                    continue;

                objects.Add(asset);
            }

            if (objects.Count == 0 || objects.Count > 1)
                throw new Exception();

            return objects.First();
#endif
            return null;
        }

        private static IAssetEnvironment _environment;

        public static IAssetEnvironment Environment => GetOrCreateEnvironment();

        private static IAssetEnvironment GetOrCreateEnvironment()
        {
            if (_environment != null)
                return _environment;

#if UNITY_EDITOR
            var config = FindAsset<InternalScopeConfig>();

            var optionsRegistry = config.AssetsStorage.Options[config.Platform];
            optionsRegistry.CacheRegistry();
            optionsRegistry.AddOptions(new PlatformOptions(config.Platform, Application.isMobilePlatform));

            var assets = new AssetEnvironment(config.AssetsStorage, optionsRegistry);
            return assets;
#endif
            return null;
        }
    }
}