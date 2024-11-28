using System.Collections.Generic;
using Internal;

namespace Global.UI
{
    public interface INavigationTarget
    {
        IReadOnlyDictionary<Side, INavigationTarget> Targets { get; }
        
        void Inc();
        void Select();
        void Deselect();
        void PerformClick();
    }
}