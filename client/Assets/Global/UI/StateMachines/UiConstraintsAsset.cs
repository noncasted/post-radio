using System.Collections.Generic;
using Global.Inputs;
using UnityEngine;

namespace Global.UI
{
    public class UiConstraintsAsset : ScriptableObject, IUIConstraints
    {
        [SerializeField] private InputConstraintsDictionary _input;

        public IReadOnlyDictionary<InputConstraints, bool> Input => _input.Value;
    }
}