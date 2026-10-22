namespace Credicarro.Webhooks.Models;

#pragma warning disable S3996

/// <summary>
/// Cadastro de webhook (<c>POST v1/webhook</c>).
/// </summary>
/// <param name="Type">Evento notificado (<c>tipo</c>).</param>
/// <param name="Url">URL HTTPS pública, com até 2048 caracteres (<c>url</c>).</param>
/// <param name="AuthenticationType">Autenticação enviada ao chamar a URL (<c>tipo_autenticacao</c>).</param>
/// <param name="Credential">Credencial do header <c>Authorization</c> (<c>credencial</c>): obrigatória em Basic (Base64 de <c>usuario:senha</c>) e Jwt (o token).</param>
public sealed record CreateWebhookRequest(
    WebhookType Type,
    string Url,
    WebhookAuthenticationType AuthenticationType,
    string? Credential = null)
{
    /// <summary>
    /// Nunca inclui a credencial.
    /// </summary>
    public override string ToString() =>
        $"CreateWebhookRequest {{ Type = {Type}, Url = {Url}, AuthenticationType = {AuthenticationType}, Credential = *** }}";
}

/// <summary>
/// Resultado do cadastro do webhook.
/// </summary>
/// <param name="WebhookId">Identificador do webhook (<c>id_webhook</c>).</param>
/// <param name="SigningSecret">
/// Segredo de assinatura (<c>segredo_assinatura</c>), usado para validar o header <c>X-CrediCarro-Signature</c>.
/// É devolvido só nesta resposta: guarde-o agora.
/// </param>
public sealed record CreateWebhookResult(Guid WebhookId, string SigningSecret)
{
    /// <summary>
    /// Nunca inclui o segredo de assinatura.
    /// </summary>
    public override string ToString() => $"CreateWebhookResult {{ WebhookId = {WebhookId}, SigningSecret = *** }}";
}

/// <summary>
/// Edição de webhook (<c>PUT v1/webhook/{id}</c>). A autenticação não pode ser alterada: exclua e cadastre outro.
/// </summary>
/// <param name="Type">Evento notificado (<c>tipo</c>).</param>
/// <param name="Url">URL HTTPS pública (<c>url</c>).</param>
/// <param name="Status">Novo status (<c>status</c>); quando omitido, mantém o atual.</param>
public sealed record UpdateWebhookRequest(WebhookType Type, string Url, WebhookStatus? Status = null);

/// <summary>
/// Webhook cadastrado. A credencial e o segredo de assinatura não são devolvidos.
/// </summary>
public sealed record Webhook(
    Guid Id,
    WebhookType Type,
    string Url,
    WebhookAuthenticationType AuthenticationType,
    WebhookStatus Status);
