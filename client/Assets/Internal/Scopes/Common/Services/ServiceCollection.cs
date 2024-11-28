using System;
using System.Collections.Generic;
using VContainer;

namespace Internal
{
    public class ServiceCollection : IServiceCollection
    {
        private readonly List<InstanceInjection> _injections = new();

        private readonly List<RegistrationBuilder> _selfResolvable = new();
        private readonly List<RegistrationBuilder> _builders = new();

        public void PassRegistrations(IContainerBuilder builder)
        {
            foreach (var registration in _builders)
                builder.Register(registration);
        }

        public void Resolve(IObjectResolver resolver)
        {
            foreach (var registration in _selfResolvable)
                resolver.Resolve(registration.ImplementationType);

            foreach (var injection in _injections)
                injection.Inject(resolver);
        }

        public void AddSelfResolvable(RegistrationBuilder builder)
        {
            _selfResolvable.Add(builder);
        }

        public void AddBuilder(RegistrationBuilder builder)
        {
            _builders.Add(builder);
        }

        public void Inject<T>(T component)
        {
            if (component == null)
                throw new NullReferenceException("No component provided");

            var injection = new InstanceInjection(component);

            _injections.Add(injection);
        }
    }
}