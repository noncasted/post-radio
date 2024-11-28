using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public class DomainConstructor
    {
        public IReadOnlyList<IAssembly> Construct()
        {
            var rawAssemblies = GetAllRawAssemblies();

            var assemblies = new List<Assembly>();
            var idsToAssemblies = new Dictionary<string, Assembly>();
            var assemblyToReferences = new Dictionary<Assembly, List<IAssembly>>();

            foreach (var rawAssembly in rawAssemblies)
            {
                var assemblyName = rawAssembly.FilePath.Split("/")[^1].Replace(".asmdef", "");
                var id = AssetDatabase.AssetPathToGUID(rawAssembly.AssetPath);
                var references = new List<IAssembly>();
                var fullPathName = rawAssembly.AssetPath
                    .Replace("Assets/", "").Replace(".asmdef", "");
                var fileName = assemblyName.Split(".")[^1];

                var path = new AssemblyPath(fileName, assemblyName, fullPathName, rawAssembly.AssetPath);
                var file = rawAssembly.File;

                var details = new AssemblyDetails(
                    rawAssembly.Namespaces,
                    rawAssembly.Usings,
                    Array.Empty<string>(),
                    rawAssembly.IsOwned);

                var toggles = new AssemblyToggles(
                    file.allowUnsafeCode,
                    file.overrideReferences,
                    file.autoReferenced,
                    file.noEngineReferences);

                var defines = new AssemblyDefines(
                    file.includePlatforms,
                    file.excludePlatforms,
                    file.precompiledReferences,
                    file.defineConstraints,
                    file.versionDefines);

                var assembly = new Assembly(id, path, references, details, toggles, defines);
                assemblies.Add(assembly);
                assemblyToReferences.Add(assembly, references);
                idsToAssemblies.Add(id, assembly);
            }

            foreach (var rawAssembly in rawAssemblies)
            {
                var id = AssetDatabase.AssetPathToGUID(rawAssembly.AssetPath);
                var assembly = idsToAssemblies[id];
                var references = assemblyToReferences[assembly];

                foreach (var reference in rawAssembly.References)
                {
                    if (idsToAssemblies.TryGetValue(reference.Id, out var referencedAssembly) == false)
                    {
                        var pathToUnknown = AssetDatabase.GUIDToAssetPath(reference.Id);
                        var unknownName = pathToUnknown.Split("/")[^1].Replace(".asmdef", "");
                        var fullPathName = reference.AssetPath
                            .Replace("Assets/", "").Replace(".asmdef", "");

                        var fileName = unknownName.Split(".")[^1];
                        var file = rawAssembly.File;
                        var path = new AssemblyPath(fileName, unknownName, fullPathName, reference.AssetPath);

                        var unknownDetails = new AssemblyDetails(
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            false);

                        var toggles = new AssemblyToggles(
                            file.allowUnsafeCode,
                            file.overrideReferences,
                            file.autoReferenced,
                            file.noEngineReferences);

                        var defines = new AssemblyDefines(
                            file.includePlatforms,
                            file.excludePlatforms,
                            file.precompiledReferences,
                            file.defineConstraints,
                            file.versionDefines);

                        referencedAssembly = new Assembly(
                            reference.Id,
                            path,
                            new List<IAssembly>(),
                            unknownDetails,
                            toggles,
                            defines);

                        idsToAssemblies.Add(reference.Id, referencedAssembly);
                        assemblies.Add(referencedAssembly);
                    }

                    references.Add(referencedAssembly);
                }
            }

            ValidateCyclicDependencies(assemblies);

            return assemblies;
        }

        private void ValidateCyclicDependencies(IReadOnlyList<IAssembly> assemblies)
        {
            var allReferences = new Dictionary<IAssembly, List<IAssembly>>();

            foreach (var assembly in assemblies)
            {
                allReferences.Add(assembly, new List<IAssembly>());

                foreach (var reference in assembly.References)
                    allReferences[assembly].Add(reference);
            }

            foreach (var (source, references) in allReferences)
            {
                foreach (var target in references)
                {
                    var targetReferences = allReferences[target];

                    foreach (var targetReference in targetReferences)
                    {
                        if (targetReference.Equals(source) == true)
                        {
                            throw new Exception(
                                $"Cyclic dependency: {source.Path.Name} -> {targetReference.Path.Name}");
                        }
                    }
                }
            }
        }

        private IReadOnlyList<RawAssembly> GetAllRawAssemblies()
        {
            var assemblies = new List<RawAssembly>();

            AddAssemblies("/Features/GamePlay", true);
            AddAssemblies("/Features/Loop", true);
            AddAssemblies("/Features/Menu", true);
            AddAssemblies("/Global", true);
            AddAssemblies("/Plugins", false);
            AddLibraryAssemblies();
            AddPackageAssemblies();

            return assemblies;

            void AddAssemblies(string directory, bool isOwned)
            {
                var path = Application.dataPath + directory;
                var directoryAssemblies = GetDirectoryAssemblies(path, isOwned);
                assemblies.AddRange(directoryAssemblies);
            }

            void AddLibraryAssemblies()
            {
                var path = Application.dataPath.Replace("Assets", "Library/PackageCache");
                var directoryAssemblies = GetDirectoryAssemblies(path, false);
                assemblies.AddRange(directoryAssemblies);
            }

            void AddPackageAssemblies()
            {
                var path = Application.dataPath.Replace("Assets", "Packages");
                var directoryAssemblies = GetDirectoryAssemblies(path, false);
                assemblies.AddRange(directoryAssemblies);
            }
        }

        private IReadOnlyList<RawAssembly> GetDirectoryAssemblies(string path, bool isOwned)
        {
            var assemblyFiles = Directory.GetFiles(path, "*.asmdef", SearchOption.AllDirectories);
            var assemblies = new List<RawAssembly>();

            foreach (var assemblyPath in assemblyFiles)
            {
                var rawAssembly = GetAssembly(assemblyPath);
                var id = AssetDatabase.AssetPathToGUID(rawAssembly.AssetPath);

                if (string.IsNullOrEmpty(id) == true)
                    continue;

                assemblies.Add(rawAssembly);
            }

            return assemblies.ToArray();

            RawAssembly GetAssembly(string asmdefPath)
            {
                asmdefPath = ConvertToUnifiedPath(asmdefPath);
                var jsonContent = File.ReadAllText(asmdefPath);
                var file = JsonConvert.DeserializeObject<AssemblyFile>(jsonContent);

                var directory = GetParentDirectory(asmdefPath);
                var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

                var namespaces = new List<string>();
                var usings = new List<string>();

                foreach (var filePath in codeFiles)
                {
                    var convertedFilePath = ConvertToUnifiedPath(filePath);
                    var fileNamespace = GetNameSpace(convertedFilePath);
                    var fileUsings = GetUsings(convertedFilePath);
                    usings.AddRange(fileUsings);

                    if (fileNamespace == string.Empty)
                        continue;

                    if (namespaces.Contains(fileNamespace) == false)
                        namespaces.Add(fileNamespace);
                }

                var assetPath = asmdefPath.Replace(Application.dataPath, "Assets");

                return new RawAssembly(
                    asmdefPath,
                    assetPath,
                    directory,
                    namespaces,
                    usings,
                    GetReferences(asmdefPath),
                    isOwned,
                    file);
            }

            string GetNameSpace(string filePath)
            {
                var text = File.ReadLines(filePath);

                foreach (var line in text)
                {
                    if (line.Contains("namespace") == false)
                        continue;

                    var fileNamespace = line.Replace("namespace ", "").Replace("{", "").Trim();
                    return fileNamespace;
                }

                return string.Empty;
            }

            IReadOnlyList<string> GetUsings(string filePath)
            {
                var lines = File.ReadLines(filePath);
                var rawUsings = new List<string>();

                foreach (var line in lines)
                {
                    if (line.Contains("using") == false)
                        return rawUsings.ToArray();

                    var fileUsing = line.Replace("using ", "").Replace(";", "").Trim();
                    rawUsings.Add(fileUsing);
                }

                return rawUsings;
            }

            IReadOnlyList<AssemblyReference> GetReferences(string filePath)
            {
                var jsonContent = File.ReadAllText(filePath);
                var asmdef = JsonConvert.DeserializeObject<AssemblyFile>(jsonContent);

                if (asmdef.references.Count == 0)
                    return Array.Empty<AssemblyReference>();

                var references = new List<AssemblyReference>();

                foreach (var referenceGUID in asmdef.references)
                {
                    var reference = referenceGUID.Replace("GUID:", "");

                    var assetPath = ConvertToUnifiedPath(AssetDatabase.GUIDToAssetPath(reference));

                    if (assetPath == string.Empty)
                        continue;

                    references.Add(new AssemblyReference(assetPath.Trim(), reference));
                }

                return references;
            }
        }

        private string GetParentDirectory(string source)
        {
            var remove = source.Split("/")[^1];
            var length = remove.Length;
            return source.Remove(source.Length - length, length);
        }

        private static string ConvertToUnifiedPath(string source)
        {
            return source.Replace("\\", "/");
        }
    }
}
