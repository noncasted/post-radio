using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private TKey[] _keys = Array.Empty<TKey>();

        [SerializeField] private TValue[] _values = Array.Empty<TValue>();

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

            _keys = new TKey[count];
            _values = new TValue[count];

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