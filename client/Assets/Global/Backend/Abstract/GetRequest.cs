using System.Collections.Generic;

namespace Global.Backend
{
    public class GetRequest : IGetRequest
    {
        public GetRequest(string url, bool withLogs, IReadOnlyList<IRequestHeader> headers)
        {
            Uri = url;
            WithLogs = withLogs;
            Headers = headers;
        }

        public string Uri { get; }
        public bool WithLogs { get; }
        public IReadOnlyList<IRequestHeader> Headers { get; }
    }
}