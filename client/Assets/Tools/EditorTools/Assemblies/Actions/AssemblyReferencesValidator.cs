using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Tools
{
    public class AssemblyReferencesValidator
    {
        [MenuItem("Tools/Assemblies/Invalidate asmdef references")]
        private static void RemoveInvalidAssemblyReferences()
        {
            var domainConstructor = new DomainConstructor();
            var assemblies = domainConstructor.Construct();

            var assemblyToNamespaces = new Dictionary<IAssembly, HashSet<string>>();
            var namespaceToAssembly = new Dictionary<string, IAssembly>();

            foreach (var assembly in assemblies)
            {
                var set = new HashSet<string>();
                assemblyToNamespaces.Add(assembly, set);

                foreach (var assemblyNamespace in assembly.Details.Namespaces)
                    set.Add(assemblyNamespace);

                foreach (var assemblyNamespace in assembly.Details.Namespaces)
                    namespaceToAssembly.TryAdd(assemblyNamespace, assembly);
            }

            foreach (var assembly in assemblies)
            {
                if (assembly.Details.IsOwned == false)
                    continue;

                var invalidReferences = new HashSet<IAssembly>();
                var assemblyUsings = assembly.Details.Usings.ToHashSet();
                var allSubReferences = new HashSet<IAssembly>();

                foreach (var reference in assembly.References)
                {
                    foreach (var subReference in reference.References)
                        allSubReferences.Add(subReference);
                }

                foreach (var reference in assembly.References)
                {
                    if (IsNamespaceValid() == false && IsSubReferenceValid() == false)
                        invalidReferences.Add(reference);
                    
                    continue;

                    bool IsNamespaceValid()
                    {
                        var name = reference.Path.Name; 
                        
                        if (name.Contains("Unity") == true || name.Contains("Pathfinding") == true)
                            return true;
                        
                        var checkNamespaces = assemblyToNamespaces[reference];

                        foreach (var checkNamespace in checkNamespaces)
                        {
                            if (assemblyUsings.Contains(checkNamespace) == true)
                                return true;
                        }

                        return false;
                    }

                    bool IsSubReferenceValid()
                    {
                        return allSubReferences.Contains(reference);
                    }
                }

                foreach (var invalidReference in invalidReferences)
                    assembly.RemoveReference(invalidReference);

                assembly.Write();
            }
        }
    }
}