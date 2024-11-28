using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    public class AddressablesScenesUnloader : ISceneUnloader
    {
        public async UniTask Unload(ISceneLoadResult result)
        {
            if (result == null)
                return;

            if (result is not ISceneInstanceProvider sceneInstanceProvider)
                return;

            await Addressables.UnloadSceneAsync(sceneInstanceProvider.SceneInstance);
        }

        public async UniTask Unload(IReadOnlyList<ISceneLoadResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            var tasks = new UniTask[results.Count];
            var scenes = new List<SceneInstance>();

            foreach (var result in results)
            {
                if (result is not ISceneInstanceProvider sceneInstanceProvider)
                    return;

                scenes.Add(sceneInstanceProvider.SceneInstance);
            }

            for (var i = 0; i < scenes.Count; i++)
                tasks[i] = Addressables.UnloadSceneAsync(scenes[i]).ToUniTask();

            await UniTask.WhenAll(tasks);
        }
    }
}