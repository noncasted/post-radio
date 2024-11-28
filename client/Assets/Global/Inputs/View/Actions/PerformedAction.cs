using System;
using UnityEngine.EventSystems;

namespace Global.Inputs
{
    public readonly struct PerformedAction
    {
        public PerformedAction(Action invokeCallback)
        {
            _invokeCallback = invokeCallback;
        }
        
        private readonly Action _invokeCallback;

        public void Invoke()
        {
            if (EventSystem.current.IsPointerOverGameObject() == true)
                return;
            
            _invokeCallback?.Invoke();
        }
    }
}