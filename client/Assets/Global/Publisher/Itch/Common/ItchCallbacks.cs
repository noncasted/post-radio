using System;
using UnityEngine;

namespace Global.Publisher.Itch
{
    [DisallowMultipleComponent]
    public class ItchCallbacks : MonoBehaviour, IJsErrorCallback
    {
        public event Action<string> Exception; 
        
        public void OnException(string exception)
        {
            Exception?.Invoke(exception);
        }
    }
}