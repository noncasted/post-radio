using System.Collections.Generic;

namespace Global.Backend
{
    public interface IGetRequest
    {
        string Uri { get; }
        bool WithLogs { get; }
        IReadOnlyList<IRequestHeader> Headers { get; }
    }
}