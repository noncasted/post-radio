using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public interface IDataStorageLoadListener
    {
        UniTask OnDataStorageLoaded(IReadOnlyLifetime lifetime, IDataStorage dataStorage);
    }
}