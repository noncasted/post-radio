using System.Collections.Generic;

namespace Global.Backend
{
    public interface IPostRequest
    {
        string Uri { get; }
        string Body { get; }
        bool WithLogs { get; }
        IReadOnlyList<IRequestHeader> Headers { get; }
    }
}