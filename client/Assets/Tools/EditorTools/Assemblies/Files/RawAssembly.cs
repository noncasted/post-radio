using System.Collections.Generic;

namespace Tools
{
    public class RawAssembly
    {
        public RawAssembly(
            string filePath,
            string assetPath,
            string directory,
            IReadOnlyList<string> namespaces,
            IReadOnlyList<string> usings,
            IReadOnlyList<AssemblyReference> references,
            bool isOwned,
            AssemblyFile file)
        {
            FilePath = filePath;
            AssetPath = assetPath;
            Directory = directory;
            Namespaces = namespaces;
            Usings = usings;
            References = references;
            IsOwned = isOwned;
            File = file;
        }
        
        public string FilePath { get; }
        public string AssetPath { get; }
        public string Directory { get; }
        public IReadOnlyList<string> Namespaces { get; }
        public IReadOnlyList<string> Usings { get; }
        public IReadOnlyList<AssemblyReference> References { get; }
        public bool IsOwned { get; }
        public AssemblyFile File { get; }
    }
}