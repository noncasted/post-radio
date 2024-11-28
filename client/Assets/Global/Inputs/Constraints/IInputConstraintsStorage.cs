using System.Collections.Generic;
using Global.UI;

namespace Global.Inputs
{
    public interface IInputConstraintsStorage
    {
        bool this[InputConstraints key] { get; }

        void Add(IReadOnlyDictionary<InputConstraints, bool> constraint);
        void Add(IUIConstraints uiConstraints);
        void Remove(IReadOnlyDictionary<InputConstraints, bool> constraint);
        void Remove(IUIConstraints uiConstraints);
    }
}