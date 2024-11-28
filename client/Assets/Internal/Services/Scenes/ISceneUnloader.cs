using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface ISceneUnloader
    {
        UniTask Unload(ISceneLoadResult result);
        UniTask Unload(IReadOnlyList<ISceneLoadResult> results);
    }
}