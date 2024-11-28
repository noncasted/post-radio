using System;
using Global.Publisher;

namespace Global.Saves
{
    
    [Serializable]
    public class VolumeSave
    {
        public float MusicVolume { get; set; } = 2;
        public float SoundVolume { get; set; } = 2;
    }
    
    public class VolumeSaveSerializer : StorageEntrySerializer<VolumeSave>
    {
        public VolumeSaveSerializer() : base("sound", new VolumeSave())
        {
        }
    }
}