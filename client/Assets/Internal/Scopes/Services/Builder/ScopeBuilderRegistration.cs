using VContainer;

namespace Internal
{
    public interface IServiceRegistration : IRegistration
    {
        IScopeBuilder Builder { get; }
    }

    public class ScopeBuilderRegistration : IServiceRegistration
    {
        public ScopeBuilderRegistration(
            IScopeBuilder builder,
            IRegistration registration)
        {
            ServiceCollection = builder.Services;
            Registration = registration.Registration;
            Builder = builder;
        }

        public IServiceCollection ServiceCollection { get; }
        public RegistrationBuilder Registration { get; }
        public IScopeBuilder Builder { get; }
    }
}