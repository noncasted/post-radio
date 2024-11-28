using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Global.Inputs
{
    public class UserInput : IUserInput
    {
        public UserInput(InputDevice device, InputUser user, Controls controls)
        {
            _device = device;
            _user = user;
            _controls = controls;
            _lifetime = new Lifetime();
        }

        private readonly InputDevice _device;
        private readonly InputUser _user;
        private readonly Controls _controls;
        private readonly Lifetime _lifetime;

        public Controls Controls => _controls;
        public IReadOnlyLifetime Lifetime => _lifetime;

        public async UniTask WaitActivation(IReadOnlyLifetime lifetime, Action callback)
        {
            await _controls.Activation.Activate.WaitPerformed(lifetime);
            callback.Invoke();
        }

        public void Dispose()
        {
            _controls.Disable();
            _controls.Dispose();
            _lifetime.Terminate();
        }
    }
}