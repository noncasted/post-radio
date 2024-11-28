using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public static class DesignExtensions
    {
        public static UniTask WaitClick(this IDesignButton button, IUIStateHandle handle)
        {
            return button.Clicked.WaitInvoke(handle.InnerLifetime);
        }
        
        public static UniTask WaitClick(this IDesignButton button, IReadOnlyLifetime lifetime)
        {
            return button.Clicked.WaitInvoke(lifetime);
        }

        public static void ListenClick(
            this IDesignButton button,
            IReadOnlyLifetime lifetime,
            Action callback)
        {
            button.Clicked.Advise(lifetime, callback);
        }
        
        public static void ListenClick(
            this IDesignButton button,
            IUIStateHandle handle,
            Action callback)
        {
            button.Clicked.Advise(handle.InnerLifetime, callback);
        }
    }
}