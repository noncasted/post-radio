using System.Collections.Generic;
using Internal;

namespace Global.Inputs
{
    public interface IInputConstructListener
    {
        void OnInputConstructed(IReadOnlyLifetime lifetime);
    }

    public class InputCallbacks
    {
        public InputCallbacks(IReadOnlyList<IInputConstructListener> listeners)
        {
            _listeners = listeners;
        }
        
        private readonly IReadOnlyList<IInputConstructListener> _listeners;
        
        public void Invoke(IReadOnlyLifetime lifetime)
        {
            foreach (var listener in _listeners)
                listener.OnInputConstructed(lifetime);
        }
    }
}