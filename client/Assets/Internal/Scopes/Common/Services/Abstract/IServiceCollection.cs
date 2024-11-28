using VContainer;

namespace Internal
{
    public interface IServiceCollection
    {
        void AddSelfResolvable(RegistrationBuilder builder);
        void AddBuilder(RegistrationBuilder builder);
        void Inject<T>(T component);
    }
}