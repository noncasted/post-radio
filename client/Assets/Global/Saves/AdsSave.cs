using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class AdsSave
    {
        public bool IsDisabled { get; set; } = false;
    }

    public class AdsSaveSerializer : StorageEntrySerializer<AdsSave>
    {
        public AdsSaveSerializer() : base("ads", new AdsSave())
        {
        }
    }
}