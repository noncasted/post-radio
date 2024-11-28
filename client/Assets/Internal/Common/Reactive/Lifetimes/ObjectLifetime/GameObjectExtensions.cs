using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    public static class GameObjectExtensions
    {
        public static IReadOnlyList<T> CreateRequiredFromPrefab<T>(
            this Transform transform,
            T prefab,
            int required) where T : MonoBehaviour
        {
            var instantiated = transform.GetComponentsInChildren<T>();
            var list = instantiated.ToList();
            var current = instantiated.Length;

            var delta = required - current;
            
            if (delta < 0)
            {
                for (var i = 0; i < Mathf.Abs(delta); i++)
                {
                    Object.Destroy(instantiated[i].gameObject);
                    list.Remove(instantiated[i]);
                }
            }
            else
            {
                for (var i = 0; i < delta; i++)
                {
                    var newObject = Object.Instantiate(prefab, transform);
                    list.Add(newObject);
                }
            }

            return list;
        }
    }
}