using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class LevelsSave
    {
        public int Selected { get; set; }
        public int Unlocked { get; set; }
    }

    public class LevelsSaveSerializer : StorageEntrySerializer<LevelsSave>
    {
        public LevelsSaveSerializer() : base("levels", new LevelsSave())
        {
        }
    }
}