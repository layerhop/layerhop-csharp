using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LayerHop.Tests
{
    public sealed class Recorded
    {
        public string Method = "";
        public string PathAndQuery = "";
        public string Path = "";
        public string Query = "";
        public Dictionary<string, string> Headers = new Dictionary<string, string>();
        public string Body = "";
        public JObject Json => JObject.Parse(Body);
    }

    /// <summary>Records every request and answers from a queue (the last response repeats).</summary>
    public sealed class MockHandler : HttpMessageHandler
    {
        private readonly Queue<(int status, string body)> _responses = new Queue<(int, string)>();
        private (int status, string body) _last = (200, "{}");
        public readonly List<Recorded> Requests = new List<Recorded>();
        public Recorded Last => Requests[Requests.Count - 1];

        public MockHandler Respond(int status, string body)
        {
            _responses.Enqueue((status, body));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var rec = new Recorded
            {
                Method = request.Method.Method,
                PathAndQuery = request.RequestUri!.PathAndQuery,
                Path = request.RequestUri.AbsolutePath,
                Query = request.RequestUri.Query.TrimStart('?'),
            };
            foreach (var h in request.Headers) rec.Headers[h.Key.ToLowerInvariant()] = string.Join(",", h.Value);
            if (request.Content != null)
            {
                foreach (var h in request.Content.Headers) rec.Headers[h.Key.ToLowerInvariant()] = string.Join(",", h.Value);
                rec.Body = await request.Content.ReadAsStringAsync(ct);
            }
            Requests.Add(rec);
            if (_responses.Count > 0) _last = _responses.Dequeue();
            var res = new HttpResponseMessage((HttpStatusCode)_last.status)
            {
                Content = new StringContent(_last.body, Encoding.UTF8, "application/json"),
            };
            res.Headers.TryAddWithoutValidation("x-request-id", "req-hdr");
            return res;
        }

        public static (LayerHopClient client, MockHandler handler) Client(string body = "{}", int status = 200, LayerHopOptions? options = null)
        {
            var h = new MockHandler().Respond(status, body);
            var c = new LayerHopClient(options ?? new LayerHopOptions { BaseUrl = "https://api.test/", ApiKey = "k1" }, new HttpClient(h));
            return (c, h);
        }

        public static Dictionary<string, string> ParseQuery(string q) =>
            q.Split('&', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(kv => kv.Split('=', 2))
                .ToDictionary(p => System.Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? System.Uri.UnescapeDataString(p[1]) : "");
    }
}
