using Internal;

namespace Global.Inputs
{
    public static class GlobalInputExtensions
    {
        public static IScopeBuilder AddInput(this IScopeBuilder builder)
        {
            builder.Register<InputConstraintsStorage>()
                .As<IInputConstraintsStorage>();

            builder.Register<InputConversion>()
                .As<IInputConversion>();

            builder.Register<InputProjection>()
                .As<IInputProjection>();

            var eventSystemPrefab = builder.GetAsset<GlobalInputOptions>().EventSystemPrefab;
            builder.Instantiate(eventSystemPrefab);
            
            var controls = new Controls();

            builder.RegisterInstance(controls);
            builder.RegisterInstance(controls.GamePlay);
            builder.RegisterInstance(controls.Menu);

            builder.Register<InputCallbacks>();

            builder.Register<InputView>()
                .WithParameter(controls)
                .As<IInputView>()
                .As<IScopeSetupCompletion>();

            builder.Register<InputActions>()
                .As<IInputActions>();

            builder.Register<VirtualCursor>()
                .As<IScopeSetup>()
                .As<ICursor>();

            return builder;
        }
    }
}