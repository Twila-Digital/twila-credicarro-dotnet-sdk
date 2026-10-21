using System.Net;

namespace Credicarro.Errors;

/// <summary>
/// Lançada para <c>429 Too Many Requests</c>.
/// </summary>
public sealed class CredicarroRateLimitException : CredicarroApiException
{
    public CredicarroRateLimitException(string message, ProblemDetailsModel problemDetails, TimeSpan? retryAfter, string? rawBody = null)
        : base(message, (HttpStatusCode)429, problemDetails, rawBody)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Cabeçalho <c>Retry-After</c> da resposta, quando presente.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
