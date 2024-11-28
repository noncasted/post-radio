using System.Collections.Generic;

namespace Tools
{
    public interface IAssemblyDomain
    {
        IReadOnlyList<IAssembly> Assemblies { get; }
    }
}