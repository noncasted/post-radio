using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Internal
{
    public static class GameObjectLifetimeExtensions
    {
        public static IReadOnlyLifetime GetObjectLifetime(this MonoBehaviour monoBehaviour)
        {
            if (monoBehaviour.TryGetComponent(out IGameObjectLifetime lifetimeSource) == false)
                lifetimeSource = monoBehaviour.gameObject.AddComponent<GameObjectLifetime>();

            return lifetimeSource.GetValidLifetime();
        }

        public static void ListenClick(this Button button, UnityAction listener)
        {
            var lifetime = button.GetObjectLifetime();
            button.onClick.AddListener(listener);
            lifetime.Listen(() => button.onClick.RemoveListener(listener));
        }
    }
}