using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class NativeSceneUnloader : ISceneUnloader
    {
        public async UniTask Unload(ISceneLoadResult result)
        {
            if (result == null)
                return;

            var task = SceneManager.UnloadSceneAsync(result.Scene);

            await task.ToUniTask();
        }

        public async UniTask Unload(IReadOnlyList<ISceneLoadResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            var tasks = new UniTask[results.Count];
            var scenes = new List<Scene>();

            foreach (var result in results)
                scenes.Add(result.Scene);

            for (var i = 0; i < scenes.Count; i++)
                tasks[i] = SceneManager.UnloadSceneAsync(scenes[i]).ToUniTask();

            await UniTask.WhenAll(tasks);
        }
    }
}