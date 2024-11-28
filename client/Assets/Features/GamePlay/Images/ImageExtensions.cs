using Internal;

namespace GamePlay.Images
{
    public static class ImageExtensions
    {
        public static IScopeBuilder AddImage(this IScopeBuilder builder)
        {
            builder.Register<ImagesLoop>()
                .As<IScopeSetup>();

            builder.RegisterAsset<ImagesOptions>();

            return builder;
        }
    }
}