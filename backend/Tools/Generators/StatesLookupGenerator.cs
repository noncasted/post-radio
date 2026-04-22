using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generators
{
    [Generator]
    public class StatesLookupGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var compilationProvider = context.CompilationProvider;

            context.RegisterSourceOutput(compilationProvider, static (spc, compilation) => {
                var attributeSymbols = new List<INamedTypeSymbol>();
                var grainState = compilation.GetTypeByMetadataName("Common.GrainStateAttribute");

                if (grainState != null)
                    attributeSymbols.Add(grainState);

                var sharedGrainState = compilation.GetTypeByMetadataName("Common.SharedGrainStateAttribute");

                if (sharedGrainState != null)
                    attributeSymbols.Add(sharedGrainState);

                if (attributeSymbols.Count == 0)
                    return;

                var entries = CollectEntries(compilation, attributeSymbols);

                if (entries.Count == 0)
                    return;

                spc.AddSource("StatesLookup.g.cs", SourceText.From(GenerateStatesLookup(entries), Encoding.UTF8));

                spc.AddSource("GeneratedStatesRegistration.g.cs",
                    SourceText.From(GenerateRegistration(entries), Encoding.UTF8));
            });
        }

        private static List<GrainStateEntry> CollectEntries(
            Compilation compilation,
            List<INamedTypeSymbol> attributeSymbols)
        {
            var results = new List<GrainStateEntry>();

            foreach (var assemblySymbol in GetAllAssemblies(compilation))
            {
                VisitNamespace(assemblySymbol.GlobalNamespace, attributeSymbols, results);
            }

            return results;
        }

        private static IEnumerable<IAssemblySymbol> GetAllAssemblies(Compilation compilation)
        {
            yield return compilation.Assembly;

            foreach (var reference in compilation.References)
            {
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference);

                if (symbol is IAssemblySymbol assemblySymbol)
                    yield return assemblySymbol;
            }
        }

        private static void VisitNamespace(
            INamespaceSymbol ns,
            List<INamedTypeSymbol> attributeSymbols,
            List<GrainStateEntry> results)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                VisitType(type, attributeSymbols, results);
            }

            foreach (var childNs in ns.GetNamespaceMembers())
            {
                VisitNamespace(childNs, attributeSymbols, results);
            }
        }

        private static void VisitType(
            INamedTypeSymbol type,
            List<INamedTypeSymbol> attributeSymbols,
            List<GrainStateEntry> results)
        {
            foreach (var attr in type.GetAttributes())
            {
                if (attr.AttributeClass == null)
                    continue;

                bool isMatch = false;

                foreach (var attributeSymbol in attributeSymbols)
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                    continue;

                string tableName = string.Empty;
                string stateName = string.Empty;
                string lookupName = string.Empty;
                string keyType = "Guid";
                bool hasTableName = false;
                bool hasStateName = false;
                bool hasLookupName = false;

                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Table")
                    {
                        tableName = (namedArg.Value.Value as string) ?? string.Empty;
                        hasTableName = true;
                    }
                    else if (namedArg.Key == "State")
                    {
                        stateName = (namedArg.Value.Value as string) ?? string.Empty;
                        hasStateName = true;
                    }
                    else if (namedArg.Key == "Lookup")
                    {
                        lookupName = (namedArg.Value.Value as string) ?? string.Empty;
                        hasLookupName = true;
                    }
                    else if (namedArg.Key == "Key")
                    {
                        if (namedArg.Value.Value is int intVal)
                        {
                            if (intVal == 100)
                                keyType = "Integer";
                            else if (intVal == 200)
                                keyType = "String";
                            else if (intVal == 300)
                                keyType = "Guid";
                            else if (intVal == 400)
                                keyType = "IntegerAndString";
                            else if (intVal == 500)
                                keyType = "GuidAndString";
                        }
                    }
                }

                if (!hasTableName || !hasStateName || !hasLookupName)
                    continue;

                var fullTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                results.Add(new GrainStateEntry(fullTypeName, tableName, stateName, keyType, lookupName));
            }

            foreach (var nested in type.GetTypeMembers())
            {
                VisitType(nested, attributeSymbols, results);
            }
        }

        private static string GenerateStatesLookup(List<GrainStateEntry> entries)
        {
            var seen = new HashSet<string>();
            var deduped = new List<GrainStateEntry>();

            foreach (var entry in entries)
            {
                if (seen.Add(entry.LookupName))
                    deduped.Add(entry);
            }

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Common;");
            sb.AppendLine();
            sb.AppendLine("public static partial class StatesLookup {");
            sb.AppendLine("    public class Info {");
            sb.AppendLine("        public required string TableName { get; init; }");
            sb.AppendLine("        public required string StateName { get; init; }");
            sb.AppendLine("        public required GrainKeyType KeyType { get; init; }");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var entry in deduped)
            {
                sb.AppendLine("    public static readonly Info " + entry.LookupName + " = new() {");
                sb.AppendLine("        TableName = \"" + entry.TableName + "\",");
                sb.AppendLine("        StateName = \"" + entry.StateName + "\",");
                sb.AppendLine("        KeyType = GrainKeyType." + entry.KeyType);
                sb.AppendLine("    };");
                sb.AppendLine();
            }

            sb.AppendLine("    public static IReadOnlyList<Info> All =>");
            sb.AppendLine("    [");

            foreach (var entry in deduped)
            {
                sb.AppendLine("        " + entry.LookupName + ",");
            }

            sb.AppendLine("    ];");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateRegistration(List<GrainStateEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Common;");
            sb.AppendLine();
            sb.AppendLine("public static class GeneratedStatesRegistration {");
            sb.AppendLine("    public static void AddAllStates(List<Infrastructure.State.GrainStateInfo> states) {");

            foreach (var entry in entries)
            {
                sb.AppendLine("        states.Add(new Infrastructure.State.GrainStateInfo {");
                sb.AppendLine("            TableName = \"" + entry.TableName + "\",");
                sb.AppendLine("            KeyType = GrainKeyType." + entry.KeyType + ",");
                sb.AppendLine("            Type = typeof(" + entry.FullTypeName + "),");
                sb.AppendLine("            Name = \"" + entry.StateName + "\"");
                sb.AppendLine("        });");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private class GrainStateEntry
        {
            public GrainStateEntry(
                string fullTypeName,
                string tableName,
                string stateName,
                string keyType,
                string lookupName)
            {
                FullTypeName = fullTypeName;
                TableName = tableName;
                StateName = stateName;
                KeyType = keyType;
                LookupName = lookupName;
            }

            public string FullTypeName { get; }
            public string TableName { get; }
            public string StateName { get; }
            public string KeyType { get; }
            public string LookupName { get; }
        }
    }
}