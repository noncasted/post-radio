using System.Collections.Concurrent;

namespace Infrastructure.Orleans;

public class GrainTypeExtractor
{
    private readonly ConcurrentDictionary<string, string> _typeNameToBaseClass = new();

    /// <summary>
    ///     These chars are delimiters when used to extract a class base type from a class
    ///     that is either <see cref="Type.AssemblyQualifiedName" /> or <see cref="Type.FullName" />.
    ///     <see cref="Extract(string)" />.
    /// </summary>
    private static readonly char[] BaseClassExtractionSplitDelimeters = ['[', ']'];

    public string Extract(string typeName)
    {
        if (_typeNameToBaseClass.TryGetValue(typeName, out var baseClass))
            return baseClass;

        var result = Parse(typeName);
        _typeNameToBaseClass[typeName] = result;

        return result;

        string Parse(string name)
        {
            var genericPosition = name.IndexOf("`", StringComparison.OrdinalIgnoreCase);

            if (genericPosition != -1)
            {
                //The following relies the generic argument list to be in form as described
                //at https://msdn.microsoft.com/en-us/library/w3f99sx1.aspx.
                var split = name.Split(BaseClassExtractionSplitDelimeters, StringSplitOptions.RemoveEmptyEntries);
                var stripped = new Queue<string>(
                    split.Where(i => i.Length > 1 && i[0] != ',').Select(WithoutAssemblyVersion)
                );

                return ReformatClassName(stripped);
            }

            return name;
        }

        string WithoutAssemblyVersion(string input)
        {
            var asmNameIndex = input.IndexOf(',');

            if (asmNameIndex >= 0)
            {
                var asmVersionIndex = input.IndexOf(',', asmNameIndex + 1);
                if (asmVersionIndex >= 0) return input[..asmVersionIndex];
                return input[..asmNameIndex];
            }

            return input;
        }

        string ReformatClassName(Queue<string> segments)
        {
            var simpleTypeName = segments.Dequeue();
            var arity = GetGenericArity(simpleTypeName);


            if (arity <= 0)
                return simpleTypeName;

            var args = new List<string>(arity);

            for (var i = 0; i < arity; i++)
                args.Add(ReformatClassName(segments));

            return $"{simpleTypeName}[{string.Join(",", args.Select(arg => $"[{arg}]"))}]";
        }

        int GetGenericArity(string input)
        {
            var arityIndex = input.IndexOf("`", StringComparison.OrdinalIgnoreCase);

            if (arityIndex != -1)
                return int.Parse(input.AsSpan()[(arityIndex + 1)..]);

            return 0;
        }
    }
}