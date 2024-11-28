using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.UI
{
    public interface IUIStateHandle
    {
        IReadOnlyLifetime InnerLifetime { get; }
        IReadOnlyLifetime OuterLifetime { get; }
        IViewableProperty<bool> IsVisible { get; }

        IUIState State { get; }
        UniTaskCompletionSource Completion { get; }

        void Exit();
    }

    public static class UIStateHandleExtensions
    {
        public static void AttachGameObject(this IUIStateHandle handle, GameObject target)
        {
            handle.IsVisible.View(handle.InnerLifetime, target.SetActive);
        }
    }
}