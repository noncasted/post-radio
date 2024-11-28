namespace Internal
{
    public class PlatformOptions : IOptionsEntry
    {
        public PlatformOptions(PlatformType platformType, bool isMobile)
        {
            PlatformType = platformType;
            IsMobile = isMobile;
        }

        public PlatformType PlatformType { get; }
        public bool IsMobile { get; }

        public bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#endif
                return false;
            }
        }
    }
}