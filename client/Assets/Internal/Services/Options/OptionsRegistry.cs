using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "OptionsRegistry", menuName = "Setup/Options/OptionsRegistry")]
    public class OptionsRegistry : ScriptableObject
    {
        [SerializeField] private List<OptionsEntry> _options;

        private readonly Dictionary<Type, IOptionsEntry> _entries = new();

        public void CacheRegistry()
        {
            _entries.Clear();
            
            foreach (var entry in _options)
            {
                var type = entry.GetType();
                _entries.Add(type, entry);
            }
        }

        public void AddOptions<T>(T options) where T : IOptionsEntry
        {
            _entries.Add(typeof(T), options);
        }
        
        public bool TryGetEntry<T>(out T value) where T : class, IOptionsEntry
        {
            if (_entries.TryGetValue(typeof(T), out var result) == true)
            {
                value = result as T;

                return true;
            }

            value = null;
            return false;
        }
    }
}