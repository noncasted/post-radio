using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class OptionsDictionary : Dictionary<PlatformType, OptionsRegistry>, ISerializationCallbackReceiver
    {
        [SerializeField] private PlatformType[] _keys = Array.Empty<PlatformType>();
        [SerializeField] private OptionsRegistry[] _values = Array.Empty<OptionsRegistry>();

        public void OnAfterDeserialize()
        {
            Clear();

            if (_values == null || _keys == null)
                return;

            for (var i = 0; i < _keys.Length && i < _values.Length; i++)
                this[_keys[i]] = _values[i];
        }

        public void OnBeforeSerialize()
        {
            var count = Count;

            _keys = new PlatformType[count];
            _values = new OptionsRegistry[count];

            var i = 0;

            foreach (var item in this)
            {
                _keys[i] = item.Key;
                _values[i] = item.Value;

                i++;
            }
        }
    }
}