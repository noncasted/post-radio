using System;
using System.Collections.Generic;
using Internal;

namespace Global.Systems
{
    public class MessageBroker : IMessageBroker
    {
        private readonly Dictionary<Type, INotifier> _notifiers = new();

        public void Publish<T>(T message)
        {
            var type = typeof(T);

            if (_notifiers.TryGetValue(type, out var notifier) == false)
                return;

            notifier.Publish(message);
        }

        public void Listen<T>(IReadOnlyLifetime lifetime, Action<T> listener)
        {
            var type = typeof(T);

            if (_notifiers.TryGetValue(type, out var notifier) == false)
            {
                notifier = new Notifier<T>();
                _notifiers.Add(type, notifier);
            }

            notifier.AddListener(listener);

            lifetime.Listen(() => notifier.RemoveListener(listener));
        }

        private interface INotifier
        {
            void AddListener(object rawListener);
            void RemoveListener(object rawListener);
            void Publish(object rawPayload);
        }

        private class Notifier<T> : INotifier
        {
            private readonly ModifiableList<Action<T>> _listeners = new();

            public void AddListener(object rawListener)
            {
                if (rawListener is not Action<T> listener)
                    return;

                _listeners.Add(listener);
            }

            public void RemoveListener(object rawListener)
            {
                if (rawListener is not Action<T> listener)
                    return;

                _listeners.Remove(listener);
            }

            public void Publish(object rawPayload)
            {
                if (rawPayload is not T payload)
                    return;

                foreach (var listener in _listeners)
                    listener?.Invoke(payload);
            }
        }
    }
}