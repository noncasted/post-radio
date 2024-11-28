using System;
using System.Collections.Generic;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class UpgradesSave
    {
        public Dictionary<string, int> KeysToLevels { get; set; } = new();
    }
    
    public class UpgradesSaveSerializer : StorageEntrySerializer<UpgradesSave>
    {
        public UpgradesSaveSerializer() : base("upgrades", new UpgradesSave())
        {
        }
    }
}