using System;
using VContainer;

namespace Internal
{
    public interface IEventResolver
    {
        Type Type { get; }
    }
    
    public interface IScopeEventListeners
    {
        void AddResolver(IEventResolver resolver);
        
        void AddListener<T>(T listener) where T : class;
        void Register(IContainerBuilder builder);
    }

    public interface IEventCollection<T> where T : class
    {
        void Add(T listener);
    }

    public static class ScopeEventListenersExtensions
    {
        public static IScopeBuilder AddViewEvents<T>(this IScopeBuilder builder, T target) where T : class
        {
            if (target is IScopeBaseSetup baseSetup)
                builder.Events.AddListener(baseSetup);

            if (target is IScopeBaseSetupAsync baseSetupAsync)
                builder.Events.AddListener(baseSetupAsync);

            if (target is IScopeSetup setup)
                builder.Events.AddListener(setup);

            if (target is IScopeSetupAsync setupAsync)
                builder.Events.AddListener(setupAsync);

            if (target is IScopeSetupCompletion setupCompletion)
                builder.Events.AddListener(setupCompletion);

            if (target is IScopeSetupCompletionAsync setupCompletionAsync)
                builder.Events.AddListener(setupCompletionAsync);

            if (target is IScopeDispose dispose)
                builder.Events.AddListener(dispose);

            if (target is IScopeDisposeAsync disposeAsync)
                builder.Events.AddListener(disposeAsync);

            return builder;
        }
    }
}