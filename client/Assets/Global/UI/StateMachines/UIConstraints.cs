using System.Collections.Generic;
using Global.Inputs;

namespace Global.UI
{
    public class UIConstraints : IUIConstraints
    {
        public UIConstraints()
        {
            Input = new Dictionary<InputConstraints, bool>();
        }
        
        public UIConstraints(InputConstraints input)
        {
            Input = new Dictionary<InputConstraints, bool>()
            {
                { input, false }
            };
        }

        public UIConstraints(IReadOnlyDictionary<InputConstraints, bool> input)
        {
            Input = input;
        }

        public IReadOnlyDictionary<InputConstraints, bool> Input { get; }
        
        public static UIConstraints Empty => new UIConstraints();
    }
}