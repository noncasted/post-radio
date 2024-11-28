using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class AssetsStorage : ScriptableObject, IAssetsStorage
    {
        [SerializeField] private AssetsDictionary _assets;
        [SerializeField] private OptionsDictionary _options;

        public IReadOnlyDictionary<string, EnvAsset> Assets => _assets;
        public IReadOnlyDictionary<PlatformType, OptionsRegistry> Options => _options;

        [Button]
        public void Scan()
        {
#if UNITY_EDITOR
            _assets.Clear();

            AssetDatabase.Refresh();
            var all = GetAssets();
            var index = GetMaxIndex();

            foreach (var asset in all)
            {
                try
                {
                    if (asset is IEnvAssetKeyOverride keyOverride)
                        _assets[keyOverride.GetKeyOverride()] = asset;
                    else
                        _assets[asset.GetType().FullName!] = asset;

                    if (asset is IEnvAssetValidator validator)
                        validator.OnValidation();

                    if (asset.Id == -1)
                    {
                        asset.SetId(index);
                        index++;
                    }

                    EditorUtility.SetDirty(asset);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to process asset: {asset.name} : {e}");
                }
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            IReadOnlyList<EnvAsset> GetAssets()
            {
                var items = AssetDatabase.FindAssets("t:EnvAsset", new[] { "Assets/" }).ToArray();
                var assets = new List<EnvAsset>();

                foreach (var guid in items)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);
                    assets.Add(asset);
                }

                return assets;
            }

            int GetMaxIndex()
            {
                var maxIndex = -1;

                foreach (var asset in all)
                {
                    if (asset.Id > maxIndex)
                        maxIndex = asset.Id;
                }

                return maxIndex;
            }
#endif
        }

#if UNITY_EDITOR
        public static class StorageScanner
        {
            private static bool _isScanning;

            [MenuItem("Assets/Scan assets %w", priority = -1000)]
            public static void ScanAssets()
            {
                if (_isScanning == true)
                    return;

                var ids = AssetDatabase.FindAssets("t:AssetsStorage");

                if (ids.Length == 0 || ids.Length > 1)
                    throw new Exception();

                _isScanning = true;

                var path = AssetDatabase.GUIDToAssetPath(ids[0]);
                var storage = AssetDatabase.LoadAssetAtPath<AssetsStorage>(path);

                try
                {
                    storage.Scan();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                _isScanning = false;
            }
        }
#endif
    }
}