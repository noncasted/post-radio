using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public static class SceneServicesExtensions
    {
        public static T[] GetComponentsInScene<T>(this MonoBehaviour source)
        {
            var rootObjects = source.gameObject.scene.GetRootGameObjects();
            var components = new List<T>();
            
            foreach (var rootObject in rootObjects)
                components.AddRange(rootObject.GetComponentsInChildren<T>());
            
            return components.ToArray();
        }
        
        public static MonoBehaviour[] GetObjectsWithComponentInScene<T>(this MonoBehaviour source)
        {
            var rootObjects = source.gameObject.scene.GetRootGameObjects();
            var components = new List<T>();
            
            foreach (var rootObject in rootObjects)
                components.AddRange(rootObject.GetComponentsInChildren<T>());
            
            var result = new List<MonoBehaviour>();
            
            foreach (var component in components)
                result.Add(component as MonoBehaviour);
            
            return result.ToArray();
        }
    }
}