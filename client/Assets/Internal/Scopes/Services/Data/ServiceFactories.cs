using Cysharp.Threading.Tasks;

namespace Internal
{
    public abstract class ServiceFactoryBase : EnvAsset
    {
        public IScopeBuilder Process(IScopeBuilder builder)
        {
            Create(builder);

            return builder;
        }

        protected abstract void Create(IScopeBuilder builder);
    }

    public abstract class ServiceFactoryBaseAsync : EnvAsset
    {
        public async UniTask<IScopeBuilder> Process(IScopeBuilder builder)
        {
            await Create(builder);

            return builder;
        }

        protected abstract UniTask Create(IScopeBuilder builder);
    }
}