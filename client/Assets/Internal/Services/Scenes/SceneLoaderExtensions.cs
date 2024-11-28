using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class SceneLoaderExtensions
    {
        public static async UniTask<(ISceneLoadResult, T)> LoadTypedResult<T>(this ISceneLoader loader, SceneData data)
        {
            var result = await loader.Load(data);

            var rootObjects = result.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return (result, searched);
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> LoadTyped<T>(this ISceneLoader loader, SceneData data)
        {
            var result = await loader.Load(data);

            var rootObjects = result.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return searched;
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> FindOrLoadScene<T>(this IScopeBuilder utils, SceneData data)
            where T : MonoBehaviour
        {
            if (utils.IsMock != true || SceneManager.GetSceneByName(data.Scene.SceneName).IsValid() != true)
                return await utils.SceneLoader.LoadTyped<T>(data);

            var targets = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var target in targets)
            {
                if (target.gameObject.scene.name != data.Scene.SceneName)
                    continue;

                return target;
            }

            return Object.FindFirstObjectByType<T>();
        }
    }
}