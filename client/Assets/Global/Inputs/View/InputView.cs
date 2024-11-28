using System.Collections.Generic;
using Global.Systems;
using Internal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Global.Inputs
{
    public class InputView : IInputView, IScopeSetupCompletion
    {
        public InputView(
            InputCallbacks callbacks,
            IUpdater updater,
            InputActions inputActions,
            Controls controls)
        {
            _callbacks = callbacks;
            _updater = updater;
            _inputActions = inputActions;
            _controls = controls;
        }

        private readonly InputCallbacks _callbacks;
        private readonly IUpdater _updater;
        private readonly InputActions _inputActions;
        private readonly Controls _controls;

        private readonly Dictionary<InputDevice, UserInput> _userInputs = new();
        private readonly ViewableDelegate<IUserInput> _userConnected = new();
        private readonly ViewableProperty<int> _devicesCount = new();

        private IReadOnlyLifetime _lifetime;
        private InputUser _keyboardUser;
        private Controls _keyboardControls;

        public IViewableProperty<int> DevicesCount => _devicesCount;
        public IViewableDelegate<IUserInput> UserConnected => _userConnected;

        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
            _controls.Enable();
            _callbacks.Invoke(lifetime);
            _updater.Add(lifetime, _inputActions);

            InputSystem.onDeviceChange += OnDeviceChange;
            lifetime.Listen(() => InputSystem.onDeviceChange -= OnDeviceChange);

            foreach (var device in InputSystem.devices)
                AddDevice(device);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    AddDevice(device);
                    break;
                case InputDeviceChange.Removed:
                    _userInputs[device].Dispose();
                    break;
                case InputDeviceChange.Disconnected:
                    _devicesCount.Set(_devicesCount.Value - 1);
                    break;
                case InputDeviceChange.Reconnected:
                    break;
                case InputDeviceChange.Enabled:
                case InputDeviceChange.Disabled:
                case InputDeviceChange.UsageChanged:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.SoftReset:
                case InputDeviceChange.HardReset:
                default:
                    break;
            }
        }

        private void AddDevice(InputDevice device)
        {
            var controls = GetOrCreateControls();
            var user = GetOrCreateUser();

            var userInput = new UserInput(device, user, controls);
            _userInputs.Add(device, userInput);

            _userConnected.Invoke(userInput);
            //userInput.WaitActivation(_lifetime, () => _userConnected.Invoke(userInput)).Forget();

            return;

            InputUser GetOrCreateUser()
            {
                if (device.IsMouseOrKeyboard() && _keyboardUser != null)
                {
                    InputUser.PerformPairingWithDevice(device, _keyboardUser);
                    return _keyboardUser;
                }

                _devicesCount.Set(_devicesCount.Value + 1);

                var inputUser = InputUser.PerformPairingWithDevice(device);

                if (device.IsMouseOrKeyboard())
                    _keyboardUser = inputUser;

                inputUser.AssociateActionsWithUser(controls);

                return inputUser;
            }

            Controls GetOrCreateControls()
            {
                if (device.IsMouseOrKeyboard() && _keyboardControls != null)
                    return _keyboardControls;

                var controls = new Controls();
                controls.Enable();

                if (device.IsMouseOrKeyboard())
                    _keyboardControls = controls;

                return controls;
            }
        }
    }
}