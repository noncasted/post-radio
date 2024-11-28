using UnityEngine;
using VContainer;

namespace Internal
{
    public static class ScopeEventsExtensions
    {
        public static IRegistration ListenEvents<T>(this IRegistration registration, T target) where T : MonoBehaviour
        {
            if (target is IScopeBaseSetup)
                registration.AsEventListener<IScopeBaseSetup>();

            if (target is IScopeBaseSetupAsync)
                registration.AsEventListener<IScopeBaseSetupAsync>();

            if (target is IScopeSetup)
                registration.AsEventListener<IScopeSetup>();

            if (target is IScopeSetupAsync)
                registration.AsEventListener<IScopeSetupAsync>();

            if (target is IScopeSetupCompletion)
                registration.AsEventListener<IScopeSetupCompletion>();

            if (target is IScopeSetupCompletionAsync)
                registration.AsEventListener<IScopeSetupCompletionAsync>();

            if (target is IScopeDispose)
                registration.AsEventListener<IScopeDispose>();

            if (target is IScopeDisposeAsync)
                registration.AsEventListener<IScopeDisposeAsync>();

            return registration;
        }

        public static IRegistration AsEventListener<TEvent>(this IRegistration registration)
        {
            registration.As<TEvent>();

            return registration;
        }

        public static IContainerBuilder AddEvents(this IContainerBuilder builder)
        {
            builder.Register<IEventLoop, EventLoop>(VContainer.Lifetime.Scoped);

            return builder;
        }
    }
}