using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "Options_Assets", menuName = "Internal/Options/Assets")]
    public class AssetsOptions : OptionsEntry
    {
        [SerializeField] private bool _useAddressables;

        public bool UseAddressables => _useAddressables;
    }
}