using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class PlayerWeaponsSave
    {
        public string LastSelected { get; set; } = string.Empty;
    }

    public class PlayerWeaponsSaveSerializer : StorageEntrySerializer<PlayerWeaponsSave>
    {
        public PlayerWeaponsSaveSerializer() : base("playerWeapons", new PlayerWeaponsSave())
        {
        }
    }
}