using System.Collections.Generic;

namespace Global.Backend
{
    public class PostRequest : IPostRequest
    {
        public PostRequest(string url, string body, bool withLogs, IReadOnlyList<IRequestHeader> headers)
        {
            Uri = url;
            Body = body;
            WithLogs = withLogs;
            Headers = headers;
        }

        public string Uri { get; }
        public string Body { get; }
        public bool WithLogs { get; }
        public IReadOnlyList<IRequestHeader> Headers { get; }
    }
}