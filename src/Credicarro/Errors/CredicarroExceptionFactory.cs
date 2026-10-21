using System.Globalization;
using System.Net;

namespace Credicarro.Errors;

internal static class CredicarroExceptionFactory
{
    public static async Task<CredicarroException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rawBody = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        var problemDetails = ProblemDetailsModel.Parse(rawBody);
        var statusCode = response.StatusCode;
        var message = BuildMessage(statusCode, problemDetails);

        return (int)statusCode switch
        {
            401 => new CredicarroAuthenticationException(
                problemDetails.Type is null
                    ? "Não autorizado (401): o token de acesso está ausente, é inválido ou expirou."
                    : message,
                statusCode,
                problemDetails.Type),
            400 => new CredicarroValidationException(message, problemDetails, rawBody),
            422 => new CredicarroBusinessRuleException(message, problemDetails, rawBody),
            429 => new CredicarroRateLimitException(message, problemDetails, GetRetryAfter(response), rawBody),
            _ => new CredicarroApiException(message, statusCode, problemDetails, rawBody)
        };
    }

    /// <summary>
    /// Falha ao gerar o token: <c>401</c> e <c>400</c> viram <see cref="CredicarroAuthenticationException"/>;
    /// o resto segue o mapeamento normal.
    /// </summary>
    public static async Task<CredicarroException> FromTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var exception = await FromResponseAsync(response, cancellationToken).ConfigureAwait(false);

        if (statusCode is not (400 or 401))
            return exception;

        if (exception is CredicarroApiException api)
        {
            return new CredicarroAuthenticationException(
                "Falha ao gerar o token de acesso: " + api.Message,
                api.StatusCode,
                api.ErrorCode);
        }

        return exception;
    }

    private static string BuildMessage(HttpStatusCode statusCode, ProblemDetailsModel problemDetails)
    {
        var message = "A API do Credicarro retornou " + ((int)statusCode).ToString(CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(problemDetails.Type))
            message += " (" + problemDetails.Type + ")";

        if (!string.IsNullOrWhiteSpace(problemDetails.Title))
            message += ": " + problemDetails.Title;

        return message + ".";
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
            return null;

        try
        {
#if NETSTANDARD2_0
            cancellationToken.ThrowIfCancellationRequested();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response) => Http.RetryAfterHeader.From(response);
}
