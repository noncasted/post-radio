using Internal;
using UnityEngine;

namespace Global.Systems
{
    public class GlobalSystemUtilsOptions : EnvAsset
    {
        [SerializeField] private Updater _updaterPrefab;

        public Updater UpdaterPrefab => _updaterPrefab;
    }
}