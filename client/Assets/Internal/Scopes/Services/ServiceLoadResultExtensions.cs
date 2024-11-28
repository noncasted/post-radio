using VContainer;

namespace Internal
{
    public static class ServiceLoadResultExtensions
    {
        public static T Get<T>(this ILoadedScope loadResult)
        {
            return loadResult.Container.Container.Resolve<T>();
        }
    }
}