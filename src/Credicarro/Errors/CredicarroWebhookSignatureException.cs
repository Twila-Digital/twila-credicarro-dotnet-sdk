namespace Credicarro.Errors;

/// <summary>
/// Lançada quando a assinatura de um webhook não é válida (não confere com o segredo, cabeçalho malformado,
/// segredo vazio ou timestamp fora da janela de tolerância de replay) ou quando o corpo não pode ser lido.
/// </summary>
public sealed class CredicarroWebhookSignatureException : CredicarroException
{
    public CredicarroWebhookSignatureException(string message) : base(message)
    {
    }
}
