using System;

namespace Tools
{
    public class AssemblyToggles : IAssemblyToggles
    {
        public AssemblyToggles(
            bool allowUnsafeCode,
            bool overrideReferences,
            bool autoReference,
            bool noEngineReferences)
        {
            AllowUnsafeCode = allowUnsafeCode;
            OverrideReferences = overrideReferences;
            AutoReference = autoReference;
            NoEngineReferences = noEngineReferences;
        }

        public bool AllowUnsafeCode { get; set; }
        public bool OverrideReferences { get; set; }
        public bool AutoReference { get; set; }
        public bool NoEngineReferences { get; set; }

        public override string ToString()
        {
            var newLine = Environment.NewLine;
            var value =
                $"\"allowUnsafeCode\": {AllowUnsafeCode.ToString().ToLower()},{newLine}" +
                $"    \"overrideReferences\": {OverrideReferences.ToString().ToLower()},{newLine}" +
                $"    \"autoReferenced\": {AutoReference.ToString().ToLower()},{newLine}" +
                $"    \"noEngineReferences\": {NoEngineReferences.ToString().ToLower()}{newLine}";

            return value;
        }
    }
}