namespace Tools
{
    public interface IAssemblyPath
    {
        public string DirectoryName { get; }
        public string Name { get; }
        public string FullPathName { get; }
        public string Raw { get; }
    }
}