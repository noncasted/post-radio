using System;
using System.Collections.Generic;

namespace Tools
{
    public class AssemblyDefines : IAssemblyDefines
    {
        public AssemblyDefines(
            List<string> includePlatforms,
            List<string> excludePlatforms,
            List<string> precompiledReferences,
            List<string> defineConstraints,
            List<VersionDefinesObject> versionDefines
        )
        {
            IncludePlatforms = includePlatforms;
            ExcludePlatforms = excludePlatforms;
            PrecompiledReferences = precompiledReferences;
            DefineConstraints = defineConstraints;
            VersionDefines = versionDefines;
        }


        public List<string> IncludePlatforms { get; }
        public List<string> ExcludePlatforms { get; set; }
        public List<string> PrecompiledReferences { get; set; }
        public List<string> DefineConstraints { get; set; }
        public List<VersionDefinesObject> VersionDefines { get; set; }

        public override string ToString()
        {
            var value = string.Empty;
            var newLine = Environment.NewLine;

            value += $"{ListToString("includePlatforms", IncludePlatforms)},{newLine}";
            value += $"    {ListToString("excludePlatforms", ExcludePlatforms)},{newLine}";
            value += $"    {ListToString("precompiledReferences", PrecompiledReferences)},{newLine}";
            value += $"    {ListToString("defineConstraints", DefineConstraints)},{newLine}";
            value += $"    {VersionsToString("versionDefines", VersionDefines)}";

            return value;
        }

        public string ListToString(string header, IReadOnlyList<string> list)
        {
            var newLine = Environment.NewLine;

            if (list.Count == 0)
                return $"\"{header}\": []";

            var value = $"\"{header}\": [{newLine}";

            for (var i = 0; i < list.Count; i++)
            {
                value += $"        \"{list[i]}\"";

                if (i != list.Count - 1)
                    value += $",{newLine}";
            }

            value += $"{newLine}    ]";

            return value;
        }

        public string VersionsToString(string header, IReadOnlyList<VersionDefinesObject> list)
        {
            var values = new List<string>();

            foreach (var version in list)
            {
                var value =
                    "{" +
                    $"    \"name\": {version.name}," +
                    $"    \"expression\": {version.expression}," +
                    $"    \"define\": {version.define}" +
                    "}";
                
                values.Add(value);
            }


            return ListToString("versionDefines", values);
        }
    }
}