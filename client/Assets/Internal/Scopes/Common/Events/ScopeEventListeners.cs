using System;
using System.Collections.Generic;
using VContainer;

namespace Internal
{
    public class ScopeEventListeners : IScopeEventListeners
    {
        public ScopeEventListeners()
        {
            _resolvers.Add(typeof(IScopeSetup), new ScopeSetupCollection());
            _resolvers.Add(typeof(IScopeSetupCompletion), new ScopeSetupCompletionCollection());
        }

        private readonly Dictionary<Type, object> _resolvers = new();

        public IReadOnlyDictionary<Type, object> EventResolvers => _resolvers;

        public void AddResolver(IEventResolver resolver)
        {
            _resolvers.Add(resolver.Type, resolver);
        }

        public void AddListener<T>(T listener) where T : class
        {
            var type = typeof(T);
            ((IEventCollection<T>)EventResolvers[type]).Add(listener);
        }

        public void Register(IContainerBuilder builder)
        {
            foreach (var (type, resolver) in EventResolvers)
            {
                builder.RegisterInstance(resolver)
                    .As(type);
            }
        }
    }
}