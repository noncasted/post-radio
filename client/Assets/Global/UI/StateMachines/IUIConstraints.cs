using System.Collections.Generic;
using Global.Inputs;

namespace Global.UI
{
    public interface IUIConstraints
    {
        IReadOnlyDictionary<InputConstraints, bool> Input { get; }
    }
}