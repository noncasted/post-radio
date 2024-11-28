using Cysharp.Threading.Tasks;

namespace Global.Publisher
{
    public interface IDataStorage
    {
        UniTask<T> GetEntry<T>() where T : class;
        UniTask Save<T>(T data);
    }
}