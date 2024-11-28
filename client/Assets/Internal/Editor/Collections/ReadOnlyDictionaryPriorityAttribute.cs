using Sirenix.OdinInspector.Editor;

namespace Internal
{
    public class ReadOnlyDictionaryPriorityAttribute : DrawerPriorityAttribute
    {
        public ReadOnlyDictionaryPriorityAttribute() : base(0, 0, 2)
        {
        }
    }
}