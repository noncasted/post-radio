using Extensions.Abstract;

namespace Extensions
{
    public interface IViewableDictionary<TKey, TValue> :
        IEventSource<IReadOnlyLifetime, TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>
    {
        IReadOnlyLifetime GetLifetime(TKey value);
    }
}