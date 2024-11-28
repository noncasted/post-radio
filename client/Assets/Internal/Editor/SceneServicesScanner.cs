using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class SceneServicesScanner : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var targets = new List<ISceneReloadListener>();
            var scenes = GetScenes();

            foreach (var scene in scenes)
            {
                var rootObjects = scene.GetRootGameObjects();

                foreach (var rootObject in rootObjects)
                {
                    var components = rootObject.GetComponentsInChildren<ISceneReloadListener>();
                    targets.AddRange(components);
                }
            }

            foreach (var target in targets)
            {
                target.OnReload();
                EditorUtility.SetDirty(target as MonoBehaviour);
            }

            return paths;
            
            IReadOnlyList<Scene> GetScenes()
            {
                var foundScenes = new List<Scene>();

                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (scene.isLoaded)
                        foundScenes.Add(scene);
                }

                return foundScenes;
            }
        }
    }
}