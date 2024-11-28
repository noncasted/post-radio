using UnityEditor;
using UnityEngine;

namespace Internal
{
    public class EnvAssetsCreator
    {
        [MenuItem("Assets/Create from sources %q", priority = -1000)]
        private static void DestroyObject()
        {
            if (Selection.objects == null)
                return;

            foreach (var target in Selection.objects)
            {
                if (target is not MonoScript script)
                    continue;
                
                var path = AssetDatabase.GetAssetPath(target);
                var newObject = ScriptableObject.CreateInstance(script.GetClass());
                var name = path.Split("/")[^1].Replace(".cs", string.Empty);
                newObject.name = name;
                var destination = path.Replace(path.Split("/")[^1], "") + name + ".asset";
                destination = AssetDatabase.GenerateUniqueAssetPath(destination);

                AssetDatabase.CreateAsset(newObject, destination);
                AssetDatabase.ForceReserializeAssets(new[] { destination });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}