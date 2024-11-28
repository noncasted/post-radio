using Internal;

namespace Global.GameLoops
{
    public abstract class BaseGameLoopFactory : ServiceFactoryBase, IEnvAssetKeyOverride
    {
        public string GetKeyOverride()
        {
            return typeof(BaseGameLoopFactory).FullName;
        }
    }
}