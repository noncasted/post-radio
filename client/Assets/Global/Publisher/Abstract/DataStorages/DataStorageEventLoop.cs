using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using VContainer.Internal;

namespace Global.Publisher
{
    public class DataStorageEventLoop
    {
        public DataStorageEventLoop(ContainerLocal<IReadOnlyList<IDataStorageLoadListener>> listeners)
        {
            _listeners = listeners.Value;
        }

        private readonly IReadOnlyList<IDataStorageLoadListener> _listeners;

        public UniTask OnDataStorageLoaded(IReadOnlyLifetime lifetime, IDataStorage dataStorage)
        {
            var tasks = new UniTask[_listeners.Count];

            for (var i = 0; i < _listeners.Count; i++)
                tasks[i] = _listeners[i].OnDataStorageLoaded(lifetime, dataStorage);

            return UniTask.WhenAll(tasks);
        }
    }
}