using System;
using System.Collections.Generic;
using Internal;

namespace Global.Inputs
{
    [Serializable]
    public class InputConstraintsDictionary : ReadOnlyDictionary<InputConstraints, bool>
    {
        public IReadOnlyDictionary<InputConstraints, bool> Value => this;
    }
}