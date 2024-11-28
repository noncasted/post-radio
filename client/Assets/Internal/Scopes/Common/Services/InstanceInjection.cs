using VContainer;

namespace Internal
{
    public class InstanceInjection
    {
        public InstanceInjection(object target)
        {
            Target = target;
        }

        public readonly object Target;

        public void Inject(IObjectResolver resolver)
        {
            resolver.Inject(Target);
        }
    }
}