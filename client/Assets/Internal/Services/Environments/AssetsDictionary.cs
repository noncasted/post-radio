using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class AssetsDictionary : Dictionary<string, EnvAsset>, ISerializationCallbackReceiver
    {
        [SerializeField] private string[] _keys = Array.Empty<string>();
        [SerializeField] private EnvAsset[] _values = Array.Empty<EnvAsset>();

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

            _keys = new string[count];
            _values = new EnvAsset[count];

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