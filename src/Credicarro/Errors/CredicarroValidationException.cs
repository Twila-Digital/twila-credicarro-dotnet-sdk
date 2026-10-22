using System.Net;

namespace Credicarro.Errors;

/// <summary>
/// Lançada para <c>400 Bad Request</c> (dados inválidos). Use <see cref="CredicarroApiException.ErrorCode"/> e
/// <see cref="CredicarroApiException.Field"/> para saber qual campo corrigir.
/// </summary>
public sealed class CredicarroValidationException : CredicarroApiException
{
    public CredicarroValidationException(string message, ProblemDetailsModel problemDetails, string? rawBody = null)
        : base(message, HttpStatusCode.BadRequest, problemDetails, rawBody)
    {
    }
}
