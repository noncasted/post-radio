using System.Collections.Generic;

namespace Tools
{
    public interface IAssemblyDefines
    {
        List<string> IncludePlatforms { get; }
        List<string> ExcludePlatforms { get; set; }
        List<string> PrecompiledReferences { get; set; }
        List<string> DefineConstraints { get; set; }
        List<VersionDefinesObject> VersionDefines { get; set; }

        string ToString();
    }
}