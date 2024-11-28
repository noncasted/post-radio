using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class CollectionsExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        
        public static T Random<T>(this IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0)
                return default;
            
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
        
        public static UniTask InvokeAsync<T>(this IReadOnlyList<T> listeners, Func<T, UniTask> invoker)
        {
            var count = listeners.Count;
            var tasks = new UniTask[count];

            for (var i = 0; i < count; i++)
                tasks[i] = invoker.Invoke(listeners[i]);

            return UniTask.WhenAll(tasks);
        }
        
        public static void Invoke<T>(this IReadOnlyList<T> listeners, Action<T> invoker)
        {
            var count = listeners.Count;

            for (var i = 0; i < count; i++)
                invoker.Invoke(listeners[i]);
        }
    }
}