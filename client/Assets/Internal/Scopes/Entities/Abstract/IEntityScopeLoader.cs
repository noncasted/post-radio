using System;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public interface IEntityScopeLoader
    {
        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parentScope,
            IScopeEntityView view,
            Action<IEntityBuilder> construct);
    }
}