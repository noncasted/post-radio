namespace Tools
{
    public class AssemblyReference
    {
        public AssemblyReference(string assetPath, string id)
        {
            AssetPath = assetPath;
            Id = id;
        }
        
        public string AssetPath { get; set; }
        public string Id { get; set; }
    }
}