using System.Collections.Generic;

namespace Internal
{
    public interface IDoubleSideDictionary<TKey, TValue>
    {
        IReadOnlyDictionary<TKey, TValue> Values { get; }
        IReadOnlyDictionary<TValue, TKey> Keys { get; }
    }
    
    public class DoubleSideIntDictionary<TValue> : IDoubleSideDictionary<int, TValue>
    {
        public DoubleSideIntDictionary(IReadOnlyList<TValue> source)
        {
            var values = new Dictionary<int, TValue>();
            var keys = new Dictionary<TValue, int>();
            
            for (var i = 0; i < source.Count; i++)
            {
                var value = source[i];

                values.Add(i, value);
                keys.Add(value, i);
            }

            Values = values;
            Keys = keys;
        }
        
        public IReadOnlyDictionary<int, TValue> Values { get; }
        public IReadOnlyDictionary<TValue, int> Keys { get; }
    }
}