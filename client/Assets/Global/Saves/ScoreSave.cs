using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class ScoreSave
    {
        public int Last { get; set; }
        public int Max { get; set; }
    }

    public class ScoreSaveSerializer : StorageEntrySerializer<ScoreSave>
    {
        public ScoreSaveSerializer() : base("score", new ScoreSave())
        {
        }
    }
}