namespace Tools
{
    public class AssemblyPath : IAssemblyPath
    {
        public AssemblyPath(
            string directoryName,
            string name,
            string fullPathName,
            string path)
        {
            DirectoryName = directoryName;
            Name = name;
            FullPathName = fullPathName;
            Raw = path;
        }
        
        public string DirectoryName { get; }
        public string Name { get; }
        public string FullPathName { get; }
        public string Raw { get; }
    }
}