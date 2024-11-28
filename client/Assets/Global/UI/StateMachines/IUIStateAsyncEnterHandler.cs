using Cysharp.Threading.Tasks;

namespace Global.UI
{
    public interface IUIStateAsyncEnterHandler
    {
        UniTask OnEntered(IUIStateHandle handle);
    }
}