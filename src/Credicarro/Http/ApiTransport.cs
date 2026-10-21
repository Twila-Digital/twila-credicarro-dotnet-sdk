using System.Net.Http.Json;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Idempotency;
using Credicarro.Serialization;

namespace Credicarro.Http;

/// <summary>
/// Montagem, envio e leitura das requisições dos clientes de recurso.
/// </summary>
internal static class ApiTransport
{
    private const int MaxIdempotencyKeyLength = 200;

    public static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        AddIdempotencyKey(request, idempotencyKey);
        return request;
    }

    public static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string path, T body, string? idempotencyKey = null)
    {
        // Serializa direto para UTF-8, uma vez só (sem string UTF-16 intermediária). As tentativas e o replay do 401
        // reutilizam este mesmo array.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, CredicarroJsonOptions.Default);
        var content = new BufferedContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        var request = new HttpRequestMessage(method, path) { Content = content };

        AddIdempotencyKey(request, idempotencyKey);
        return request;
    }

    /// <summary>
    /// Valida uma <c>Idempotency-Key</c> informada pelo integrador: de 1 a 200 caracteres ASCII imprimíveis.
    /// </summary>
    public static string? ValidateIdempotencyKey(string? idempotencyKey, string parameterName)
    {
        if (idempotencyKey is null)
            return null;

        if (idempotencyKey.Length is < 1 or > MaxIdempotencyKeyLength || idempotencyKey.Trim().Length == 0)
            throw new ArgumentException("A Idempotency-Key deve ter de 1 a 200 caracteres.", parameterName);

        foreach (var character in idempotencyKey)
        {
            if (character < 0x20 || character > 0x7E)
                throw new ArgumentException("A Idempotency-Key deve conter apenas caracteres ASCII imprimíveis.", parameterName);
        }

        return idempotencyKey;
    }

    /// <summary>Envia a requisição e devolve o corpo desserializado.</summary>
    public static async Task<T> SendAsync<T>(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        where T : class
    {
        using (request)
        using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Envia a requisição e ignora o corpo (respostas <c>202</c>/<c>204</c>).</summary>
    public static async Task SendAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    public static Task<T> GetAsync<T>(HttpClient httpClient, string path, CancellationToken cancellationToken)
        where T : class =>
        SendAsync<T>(httpClient, CreateRequest(HttpMethod.Get, path), cancellationToken);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw await CredicarroExceptionFactory.FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        // No .NET Framework, uma resposta sem corpo pode vir com Content nulo.
        if (response.Content is null)
        {
            throw new CredicarroApiException(
                "A API do Credicarro retornou uma resposta vazia.",
                response.StatusCode,
                ProblemDetailsModel.Empty());
        }

        T? result;

        try
        {
            result = await response.Content.ReadFromJsonAsync<T>(CredicarroJsonOptions.Default, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CredicarroApiException(
                "A API do Credicarro retornou uma resposta em formato inesperado.",
                response.StatusCode,
                ProblemDetailsModel.Empty(),
                innerException: ex);
        }

        return result ?? throw new CredicarroApiException(
            "A API do Credicarro retornou uma resposta vazia.",
            response.StatusCode,
            ProblemDetailsModel.Empty());
    }

    private static void AddIdempotencyKey(HttpRequestMessage request, string? idempotencyKey)
    {
        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation(IdempotencyKeyDelegatingHandler.HeaderName, idempotencyKey);
    }
}
