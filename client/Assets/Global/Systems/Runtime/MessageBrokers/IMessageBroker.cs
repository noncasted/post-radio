using System;
using Internal;

namespace Global.Systems
{
    public interface IMessageBroker
    {
        void Publish<T>(T payload);
        void Listen<T>(IReadOnlyLifetime lifetime, Action<T> listener);
    }
}