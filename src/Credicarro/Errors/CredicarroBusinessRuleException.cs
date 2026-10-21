namespace Credicarro.Errors;

/// <summary>
/// Lançada para <c>422 Unprocessable Entity</c> (regra de negócio não atendida, por exemplo
/// <c>Store.Integration.Intermediation</c> ou <c>Customer.Account.Incomplete</c>). Trate pelo
/// <see cref="CredicarroApiException.ErrorCode"/>.
/// </summary>
public sealed class CredicarroBusinessRuleException : CredicarroApiException
{
    public CredicarroBusinessRuleException(string message, ProblemDetailsModel problemDetails, string? rawBody = null)
        : base(message, (System.Net.HttpStatusCode)422, problemDetails, rawBody)
    {
    }
}
