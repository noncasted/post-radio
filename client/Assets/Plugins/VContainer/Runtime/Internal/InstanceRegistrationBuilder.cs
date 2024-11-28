using System.Runtime.CompilerServices;

namespace VContainer.Internal
{
    public sealed class InstanceRegistrationBuilder : RegistrationBuilder
    {
        readonly object implementationInstance;

        public InstanceRegistrationBuilder(object implementationInstance)
            : base(implementationInstance.GetType(), Lifetime.Singleton)
        {
            this.implementationInstance = implementationInstance;
        }
        
        public InstanceRegistrationBuilder(object implementationInstance, Lifetime lifetime)
            : base(implementationInstance.GetType(), lifetime)
        {
            this.implementationInstance = implementationInstance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Registration Build()
        {
            var spawner = new ExistingInstanceProvider(implementationInstance);
            return new Registration(ImplementationType, Lifetime, InterfaceTypes, spawner);
        }
    }
}
