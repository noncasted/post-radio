namespace Internal
{
    public interface IInternalScopeConfig
    {
        PlatformType Platform { get; }
        InternalScope Scope { get; }
        IAssetsStorage AssetsStorage { get; }
    }
}