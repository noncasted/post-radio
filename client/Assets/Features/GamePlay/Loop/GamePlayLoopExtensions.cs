using GamePlay.Audio;
using GamePlay.Common;
using GamePlay.Images;
using Internal;

namespace GamePlay.Loop
{
    public static class GamePlayLoopExtensions
    {
        public static IScopeBuilder AddGamePlayLoop(this IScopeBuilder builder)
        {
            builder.Register<GamePlayLoop>()
                .As<IGamePlayLoop>();

            builder.RegisterAsset<BackendEndpoints>();

            builder
                .AddAudio()
                .AddImage();

            return builder;
        }
    }
}