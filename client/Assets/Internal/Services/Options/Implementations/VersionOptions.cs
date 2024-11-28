using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
    {
        [InlineEditor]
        [CreateAssetMenu(fileName = "Options_Version", menuName = "Internal/Options/Version")]
        public class VersionOptions : OptionsEntry
        {
            [SerializeField] private string _value;

            public string Value => _value;
        }
    }
