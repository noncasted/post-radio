using Internal;
using UnityEngine;

namespace GamePlay.Common
{
    public class BackendEndpoints : EnvAsset
    {
        [SerializeField] private string _url;
        
        public string Url => _url;
    }
}