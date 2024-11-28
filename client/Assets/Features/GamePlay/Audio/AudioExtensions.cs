using Internal;

namespace GamePlay.Audio
{
    public static class AudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            builder.Register<AudioLoop>()
                .As<IScopeSetup>();

            return builder;
        }
    }
}