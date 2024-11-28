using VContainer;

namespace Internal
{
    public interface IEntityRegistration : IRegistration
    {
        IEntityBuilder Builder { get; }
    }
    
    public class EntityRegistration : IEntityRegistration
    {
        public EntityRegistration(
            IEntityBuilder builder,
            IRegistration registration)
        {
            ServiceCollection = builder.Services;
            Registration = registration.Registration;
            Builder = builder;
        }

        public IServiceCollection ServiceCollection { get; }
        public RegistrationBuilder Registration { get; }
        public IEntityBuilder Builder { get; }
    }
}