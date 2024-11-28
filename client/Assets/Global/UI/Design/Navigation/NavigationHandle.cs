using Global.GameServices;
using Global.Inputs;
using Internal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Global.UI
{
    public class NavigationHandle
    {
        public NavigationHandle(ILocalUser user, INavigationTarget first)
        {
            _user = user;
            _first = first;
        }

        private readonly ILocalUser _user;
        private readonly INavigationTarget _first;

        public void Process(IReadOnlyLifetime lifetime)
        {
            var current = _first;
            current.Inc(); 
            
            var ui = _user.Input.Controls.UI;
            ui.Navigation.ListenPerformed(lifetime, OnMove);
            ui.Click.ListenPerformed(lifetime, OnClicked);

            return;

            void OnMove(InputAction.CallbackContext context)
            {
                var direction = context.ReadValue<Vector2>();
                var side = direction.ToSide();

                if (current.Targets.ContainsKey(side) == false)
                    return;

                current.Deselect();
                current = current.Targets[side];
                current.Select();
            }

            void OnClicked(InputAction.CallbackContext _)
            {
                current.PerformClick();
            }
        }
    }
}