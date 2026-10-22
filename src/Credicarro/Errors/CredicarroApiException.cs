using System.Net;

namespace Credicarro.Errors;

/// <summary>
/// Lançada para qualquer resposta de erro HTTP da API do Credicarro (400, 403, 404, 409, 422, 5xx...).
/// A mensagem traz só o status, o <c>tipo</c> e o <c>titulo</c>; <see cref="Detail"/> e <see cref="RawBody"/> ficam em
/// propriedades porque podem conter dados pessoais.
/// </summary>
public class CredicarroApiException : CredicarroException
{
    public CredicarroApiException(
        string message,
        HttpStatusCode statusCode,
        ProblemDetailsModel problemDetails,
        string? rawBody = null,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
        RawBody = rawBody;
    }

    /// <summary>
    /// Código HTTP da resposta.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Campo <c>tipo</c> do ProblemDetails (ex.: <c>Store.Integration.Intermediation</c>): string estável, mas não um enum fechado.
    /// </summary>
    public string? ErrorCode => ProblemDetails.Type;

    /// <summary>Campo <c>titulo</c> do ProblemDetails.</summary>
    public string? Title => ProblemDetails.Title;

    /// <summary>Campo <c>detalhe</c> do ProblemDetails. Pode conter dados pessoais.</summary>
    public string? Detail => ProblemDetails.Detail;

    /// <summary>Campo <c>campo</c> do ProblemDetails (campo da requisição relacionado ao erro).</summary>
    public string? Field => ProblemDetails.Field;

    /// <summary>Lista <c>erros</c> do ProblemDetails (vazia quando a API não a retornou).</summary>
    public IReadOnlyList<ProblemErrorModel> Errors => ProblemDetails.Errors;

    /// <summary>O <c>ProblemDetails</c> lido da resposta.</summary>
    public ProblemDetailsModel ProblemDetails { get; }

    /// <summary>Corpo bruto da resposta, quando houver. Pode conter dados pessoais.</summary>
    public string? RawBody { get; }
}
