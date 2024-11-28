using System;

namespace Global.Inputs
{
    public interface IInputActions
    {
        void Add(Action callback);
    }
}