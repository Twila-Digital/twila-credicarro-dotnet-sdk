using System.Text;

namespace Credicarro.UnitTests.TestUtilities;

/// <summary>
/// Requisição como chegou ao handler interno (corpo e cabeçalhos lidos no momento do envio).
/// </summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)
{
    public string Path => Uri.AbsolutePath;

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public bool HasHeader(string name) => Headers.ContainsKey(name);
}

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private int _callCount;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond)
        : this((request, callIndex, _) => respond(request, callIndex))
    {
    }

    public int CallCount => _callCount;

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var callIndex = Interlocked.Increment(ref _callCount);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
            headers[header.Key] = string.Join(",", header.Value);

        var body = string.Empty;
        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
                headers[header.Key] = string.Join(",", header.Value);

            body = Encoding.UTF8.GetString(await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
        }

        lock (Requests)
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, headers, body));

        return await respond(request, callIndex, cancellationToken).ConfigureAwait(false);
    }
}
