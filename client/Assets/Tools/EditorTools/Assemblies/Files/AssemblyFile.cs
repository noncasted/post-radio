using System.Collections.Generic;

namespace Tools
{
    public class AssemblyFile
    {
        public string name;
        
        public List<string> references = new();
        public List<string> includePlatforms = new();
        public List<string> excludePlatforms = new();
        public List<string> defineConstraints = new();
        public List<VersionDefinesObject> versionDefines = new();
        public List<string> precompiledReferences = new();
        
        public bool allowUnsafeCode;
        public bool overrideReferences;
        public bool autoReferenced;
        public bool noEngineReferences;
    }
}