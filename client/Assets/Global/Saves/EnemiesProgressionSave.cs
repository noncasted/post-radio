using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class EnemiesProgressionSave
    {
        public int LastPassed { get; set; } = 0;
    }
    
    public class EnemiesProgressionSaveSerializer : StorageEntrySerializer<EnemiesProgressionSave>
    {
        public EnemiesProgressionSaveSerializer() : base("enemiesProgression", new EnemiesProgressionSave())
        {
        }
    }
}