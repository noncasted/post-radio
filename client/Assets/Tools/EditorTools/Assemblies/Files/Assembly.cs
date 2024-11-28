using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tools
{
    public class Assembly : IAssembly
    {
        public Assembly(
            string id,
            IAssemblyPath path,
            List<IAssembly> references,
            IAssemblyDetails details,
            IAssemblyToggles toggles,
            IAssemblyDefines defines)
        {
            _references = references;
            Id = id;
            Path = path;
            Details = details;
            Toggles = toggles;
            Defines = defines;
        }

        private readonly List<IAssembly> _references;

        public string Id { get; }
        public IAssemblyPath Path { get; }
        public IReadOnlyList<IAssembly> References => _references;
        public IAssemblyDetails Details { get; }
        public IAssemblyToggles Toggles { get; }
        public IAssemblyDefines Defines { get; }

        public void AddAssembly(IAssembly assembly)
        {
            _references.Add(assembly);
        }

        public void RemoveReference(IAssembly assembly)
        {
            _references.Remove(assembly);
        }

        public void Write()
        {
            var newLine = Environment.NewLine;
            
            var json =
                "{" +
                $"{newLine}    \"name\": \"{Path.Name}\",{newLine}" +
                $"    {ReferencesToString()},{newLine}" +
                $"    {Defines.ToString()},{newLine}" +
                $"    {Toggles.ToString()}" +
                "}";

            File.WriteAllText(Path.Raw, json);
        }

        string ReferencesToString()
        {
            var value = $"\"references\": [";

            var references = new List<IAssembly>(References);
            references = references.OrderBy(t => t.Path.Name).ToList();

            for (var i = 0; i < references.Count; i++)
            {
                value += $"{Environment.NewLine}        \"GUID:{references[i].Id}\"";
                if (i != references.Count - 1)
                    value += ",";
            }

            value += $"{Environment.NewLine}    ]";

            return value;
        }

        public override bool Equals(object obj)
        {
            if (obj is not IAssembly target)
                return false;

            return target.Id == Id;
        }
    }
}