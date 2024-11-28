using System;
using System.Collections.Generic;
using Global.Systems;

namespace Global.Inputs
{
    public class InputActions : IInputActions, IUpdatable
    {
        private readonly List<PerformedAction> _actions = new();

        public void Add(Action callback)
        {
            var action = new PerformedAction(callback);
            _actions.Add(action);
        }

        public void OnUpdate(float delta)
        {
            if (_actions.Count == 0)
                return;

            foreach (var action in _actions)
                action.Invoke();
            
            _actions.Clear();
        }
    }
}