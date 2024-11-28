using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "Options_Debug", menuName = "Internal/Options/Debug")]
    public class DebugOptions : OptionsEntry
    {
        [SerializeField] private bool _enableGizmos;
        [SerializeField] private bool _enableLogs;

        public bool EnableGizmos => _enableGizmos;
        public bool EnableLogs => _enableLogs;
    }
}