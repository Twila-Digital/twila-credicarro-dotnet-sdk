namespace Credicarro.Http;

internal static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Garante que o corpo esteja em um <see cref="BufferedContent"/> (lido uma única vez). Corpos criados pelo SDK já vêm
    /// assim; outros tipos de conteúdo são materializados aqui, uma vez, e substituídos na própria requisição.
    /// </summary>
    public static async Task EnsureBufferedAsync(this HttpRequestMessage request)
    {
        if (request.Content is null or BufferedContent)
            return;

        var original = request.Content;
        var bytes = await original.ReadAsByteArrayAsync().ConfigureAwait(false);
        request.Content = new BufferedContent(bytes, original.Headers);
    }

    /// <summary>
    /// Clona a requisição compartilhando o corpo já materializado (sem copiar). Exige <see cref="EnsureBufferedAsync"/> antes.
    /// </summary>
    public static HttpRequestMessage Clone(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (request.Content is BufferedContent buffered)
            clone.Content = new BufferedContent(buffered.Bytes, buffered.Headers);
        else if (request.Content is not null)
            throw new InvalidOperationException("O corpo da requisição precisa estar materializado antes de clonar.");

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
