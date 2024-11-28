using UnityEditor;
using UnityEngine;

namespace Tools
{
    [InitializeOnLoad]
    public static class MockSwitcher
    {
        static MockSwitcher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            var mock = Object.FindFirstObjectByType<MockBase>();

            if (mock == null)
                return;

            mock.Process().Forget();
        }
    }
}