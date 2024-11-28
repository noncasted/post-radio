using Global.GameLoops;
using Internal;

namespace Loop
{
    public class GameLoopFactory : BaseGameLoopFactory
    {
        protected override void Create(IScopeBuilder builder)
        {
            builder.Register<GameLoop>()
                .WithScopeLifetime()
                .As<IGamePlayLoader>();
        }
    }
}