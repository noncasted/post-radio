using System.Collections.Generic;
using UnityEngine;

namespace Global.UI
{
    public static class DesignHierarchyExtensions
    {
        public static IReadOnlyList<T> GetComponentInChildOnly<T>(this MonoBehaviour gameObject)
        {
            var transform = gameObject.transform;
            var components = new List<T>();
            
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);

                if (child.TryGetComponent(out T component) == true)
                    components.Add(component);
            }

            return components;
        }
        
        public static IReadOnlyList<T> GetComponentInChildOnlyIncludeSelf<T>(this MonoBehaviour gameObject)
        {
            var transform = gameObject.transform;
            var components = new List<T>();
            
            if (gameObject.TryGetComponent(out T selfComponent) == true)
                components.Add(selfComponent);
            
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);

                if (child.TryGetComponent(out T component) == true)
                    components.Add(component);
            }

            return components;
        }
    }
}