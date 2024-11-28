using UnityEngine;

namespace Internal
{
    public interface IAssetEnvironment
    {
        T GetAsset<T>() where T : ScriptableObject;
        T GetOptions<T>() where T : class, IOptionsEntry;
    }
}