using System.Net;

namespace Credicarro.Errors;

/// <summary>
/// Lançada quando a autenticação falha: credenciais inválidas (<c>401 Integration.Authentication.InvalidCredentials</c>
/// ou <c>400</c> ao gerar o token) ou <c>401</c> persistente depois de renovar o token.
/// </summary>
public sealed class CredicarroAuthenticationException : CredicarroException
{
    public CredicarroAuthenticationException(string message) : base(message)
    {
    }

    public CredicarroAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CredicarroAuthenticationException(string message, HttpStatusCode statusCode, string? errorCode) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>Status HTTP da resposta que causou a falha, quando houve resposta.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Campo <c>tipo</c> do ProblemDetails (ex.: <c>Integration.Authentication.InvalidCredentials</c>), quando houve corpo.</summary>
    public string? ErrorCode { get; }
}
