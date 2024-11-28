using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class AddressablesSceneLoader : ISceneLoader
    {
        public async UniTask<ISceneLoadResult> Load(SceneData sceneAsset)
        {
            var scene = await Addressables.LoadSceneAsync(sceneAsset.Scene.Key, LoadSceneMode.Additive).ToUniTask();

            return new AddressablesSceneLoadResult(scene);
        }
    }
}