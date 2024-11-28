using System;
using Internal;

namespace Global.Systems
{
    public static class Msg
    {
        private static IMessageBroker _messageBroker;

        public static void Inject(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
        }

        public static void Publish<T>(T message)
        {
            _messageBroker.Publish(message);
        }
        
        public static void Publish<T>()
        {
            _messageBroker.Publish(default(T));
        }

        public static void Listen<T>(IReadOnlyLifetime lifetime, Action<T> listener)
        {
            _messageBroker.Listen(lifetime, listener);
        }
    }
}