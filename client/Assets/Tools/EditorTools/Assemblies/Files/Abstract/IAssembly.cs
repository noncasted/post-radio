using System.Collections.Generic;

namespace Tools
{
    public interface IAssembly
    {
        string Id { get; }
        
        IAssemblyPath Path { get; }
        IReadOnlyList<IAssembly> References { get; }
        IAssemblyDetails Details { get; }
        IAssemblyToggles Toggles { get; }
        IAssemblyDefines Defines { get; }
        
        void AddAssembly(IAssembly assembly);
        void RemoveReference(IAssembly assembly);

        void Write();
    }
}