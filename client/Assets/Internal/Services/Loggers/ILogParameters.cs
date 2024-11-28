using System.Collections.Generic;

namespace Internal
{
    public interface ILogParameters
    {
        ILogBodyParameters BodyParameters { get; }
        IReadOnlyList<ILogHeader> Headers { get; }
    }
}