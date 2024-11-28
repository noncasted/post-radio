using UnityEditor;

namespace Tools
{
    public static class ResetAssembliesEngineReferences
    {
        [MenuItem("Tools/Assemblies/Reset engine references")]
        private static void RemoveInvalidAssemblyReferences()
        {
            var domainConstructor = new DomainConstructor();
            var assemblies = domainConstructor.Construct();

            foreach (var assembly in assemblies)
            {
                if (assembly.Details.IsOwned == false)
                    continue;

                if (ContainsEngineReferences() == false)
                    assembly.Toggles.NoEngineReferences = true;

                assembly.Write();

                continue;

                bool ContainsEngineReferences()
                {
                    if (CheckUsing(assembly) == true)
                        return true;

                    foreach (var reference in assembly.References)
                    {
                        if (CheckUsing(reference) == true)
                            return true;
                    }

                    return false;
                }

                bool CheckUsing(IAssembly checkAssembly)
                {
                    foreach (var usingLine in checkAssembly.Details.Usings)
                    {
                        if (usingLine.Contains("UnityEngine") || usingLine.Contains("UnityEditor"))
                            return true;
                    }

                    return false;
                }
            }
        }
    }
}