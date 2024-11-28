using System.Collections.Generic;
using VContainer.Unity;

namespace Internal
{
    public interface IServiceScopeLoadResult
    {
        LifetimeScope Scope { get; }
        ILifetime Lifetime { get; }
        IEventLoop EventLoop { get; }
        IReadOnlyList<ISceneLoadResult> Scenes { get; }
    }
}