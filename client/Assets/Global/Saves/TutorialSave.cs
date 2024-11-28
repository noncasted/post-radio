using System;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class TutorialSave
    {
        public bool IsTutorialPassed { get; set; }
    }

    public class TutorialSaveSerializer : StorageEntrySerializer<TutorialSave>
    {
        public TutorialSaveSerializer() : base("tutorial", new TutorialSave())
        {
        }
    }
}