using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.InputSystem;

namespace Global.Inputs
{
    public static class InputExtensions
    {
        public static IViewableProperty<T> ToProperty<T>(this InputAction action, IReadOnlyLifetime lifetime)
            where T : struct
        {
            var property = new ViewableProperty<T>();
            
            action.performed += OnChanged;
            action.canceled += OnChanged;

            lifetime.Listen(() =>
            {
                action.performed -= OnChanged;
                action.canceled -= OnChanged;
            });

            return property;

            void OnChanged(InputAction.CallbackContext value)
            {
                property.Set(value.ReadValue<T>());
            }
        }
        
        public static IViewableProperty<bool> ToFlag(this InputAction action, IReadOnlyLifetime lifetime)
        {
            var property = new ViewableProperty<bool>();
            
            action.performed += OnPerformed;
            action.canceled += OnCanceled;

            lifetime.Listen(() =>
            {
                action.performed -= OnPerformed;
                action.canceled -= OnCanceled;
            });

            return property;

            void OnPerformed(InputAction.CallbackContext value)
            {
                property.Set(true);
            }

            void OnCanceled(InputAction.CallbackContext value)
            {
                property.Set(false);
            }
        }

        public static void AttachProperty<T>(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            ViewableProperty<T> property) where T : struct
        {
            action.performed += OnChanged;
            action.canceled += OnChanged;

            lifetime.Listen(() =>
            {
                action.performed -= OnChanged;
                action.canceled -= OnChanged;
            });

            return;

            void OnChanged(InputAction.CallbackContext value)
            {
                property.Set(value.ReadValue<T>());
            }
        }

        public static void AttachFlag(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            ViewableProperty<bool> property)
        {
            action.performed += OnPerformed;
            action.canceled += OnCanceled;

            lifetime.Listen(() =>
            {
                action.performed -= OnPerformed;
                action.canceled -= OnCanceled;
            });

            return;

            void OnPerformed(InputAction.CallbackContext value)
            {
                property.Set(true);
            }

            void OnCanceled(InputAction.CallbackContext value)
            {
                property.Set(false);
            }
        }

        public static void Listen(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled)
        {
            action.performed += performed;
            action.canceled += canceled;

            lifetime.Listen(() =>
            {
                action.performed -= performed;
                action.canceled -= canceled;
            });
        }

        public static void Listen(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            Action<InputAction.CallbackContext> performed)
        {
            action.performed += performed;
            action.canceled += performed;

            lifetime.Listen(() =>
            {
                action.performed -= performed;
                action.canceled -= performed;
            });
        }

        public static void ListenPerformed(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            Action<InputAction.CallbackContext> performed)
        {
            action.performed += performed;

            lifetime.Listen(() => { action.performed -= performed; });
        }

        public static void ListenPerformed(
            this InputAction action,
            IReadOnlyLifetime lifetime,
            Action performed)
        {
            action.performed += OnPerformed;

            lifetime.Listen(() => { action.performed -= OnPerformed; });

            return;

            void OnPerformed(InputAction.CallbackContext _)
            {
                performed.Invoke();
            }
        }

        public static UniTask WaitPerformed(this InputAction action, IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource();
            action.performed += OnPerformed;
            lifetime.Listen(() =>
            {
                action.performed += OnPerformed;
                completion.TrySetCanceled();
            });

            return completion.Task;

            void OnPerformed(InputAction.CallbackContext _)
            {
                completion.TrySetResult();
            }
        }

        public static bool IsMouseOrKeyboard(this InputDevice device)
        {
            return device.displayName == "Mouse" || device.displayName == "Keyboard";
        }
    }
}