using VContainer;

namespace Internal
{
    public class BaseRegistration : IRegistration
    {
        public BaseRegistration(IServiceCollection serviceCollection, RegistrationBuilder builder)
        {
            ServiceCollection = serviceCollection;
            Registration = builder;
        }

        public IServiceCollection ServiceCollection { get; }
        public RegistrationBuilder Registration { get; }
    }
}