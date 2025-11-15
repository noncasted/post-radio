using System.Collections;

namespace Common
{
    public class ModifiableList<T> : IEnumerable<T>
    {
        private readonly List<T> _list = new();
        private readonly List<T> _add = new(0);
        private readonly List<T> _remove = new(0);

        private bool _isIterated;
        
        public int Count => _list.Count;
        
        public void Add(T value)
        {
            if (_isIterated == true)
                _add.Add(value);
            else
                _list.Add(value);
        }

        public void Remove(T value)
        {
            if (_isIterated == true)
                _remove.Add(value);
            else
                _list.Remove(value);
        }

        public IEnumerator<T> GetEnumerator()
        {
            _isIterated = true;

            foreach (var value in _list)
                yield return value;

            _isIterated = false;

            foreach (var add in _add)
                _list.Add(add);

            foreach (var remove in _remove)
                _list.Remove(remove);
            
            _add.Clear();
            _remove.Clear();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _list.Clear();
            _list.TrimExcess();
        }
    }
}