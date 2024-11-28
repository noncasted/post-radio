using Internal;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Global.Inputs
{
    public class GlobalInputOptions : EnvAsset
    {
        [SerializeField] private EventSystem _eventSystemPrefab;
        
        public EventSystem EventSystemPrefab => _eventSystemPrefab;
    }
}