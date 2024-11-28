using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public interface IScopeDisposer
    {
        public UniTask Unload(IServiceScopeLoadResult scopeLoadResult);
    }
}