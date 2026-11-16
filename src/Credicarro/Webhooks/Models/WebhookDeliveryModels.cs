namespace Credicarro.Webhooks.Models;

#pragma warning disable S3996

/// <summary>
/// Filtros do histórico de envios. Valores <see langword="null"/> não são enviados.
/// </summary>
/// <param name="InitialDate">Início do período (<c>data_inicial</c>).</param>
/// <param name="FinalDate">Fim do período (<c>data_final</c>).</param>
/// <param name="ProposalId">Identificador da proposta (<c>id_proposta</c>).</param>
/// <param name="SimulationId">Identificador da simulação (<c>id_simulacao</c>).</param>
/// <param name="ProposalNumber">Número da proposta, até 50 caracteres (<c>numero_proposta</c>).</param>
/// <param name="StatusCode">Status HTTP devolvido pela sua URL, de 100 a 599 (<c>status_http</c>).</param>
public sealed record WebhookDeliveryFilter(
    DateTimeOffset? InitialDate = null,
    DateTimeOffset? FinalDate = null,
    Guid? ProposalId = null,
    Guid? SimulationId = null,
    string? ProposalNumber = null,
    int? StatusCode = null);

/// <summary>
/// Envio de webhook registrado no histórico. Use <see cref="Id"/> em <see cref="IWebhooksClient.ResendAsync"/>.
/// </summary>
/// <param name="Id">Identificador do envio (<c>id_auditoria</c>).</param>
/// <param name="WebhookId">Webhook que recebeu o envio (<c>id_webhook</c>).</param>
/// <param name="Type">Tipo do webhook (<c>tipo</c>).</param>
/// <param name="Request">Corpo enviado, como texto JSON (<c>requisicao</c>).</param>
/// <param name="Response">Corpo devolvido pela sua URL; vazio quando não houve resposta (<c>resposta</c>).</param>
/// <param name="StatusCode">Status HTTP devolvido pela sua URL; <see langword="null"/> quando ela não respondeu (<c>status_http</c>).</param>
/// <param name="CreatedAt">Data e hora do envio (<c>data_criacao</c>).</param>
public sealed record WebhookDelivery(
    Guid Id,
    Guid? WebhookId,
    WebhookType Type,
    string Request,
    string Response,
    int? StatusCode,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Nunca inclui os corpos, que podem conter dados pessoais.
    /// </summary>
    public override string ToString() =>
        $"WebhookDelivery {{ Id = {Id}, WebhookId = {WebhookId}, Type = {Type}, StatusCode = {StatusCode}, CreatedAt = {CreatedAt:o}, Request = ***, Response = *** }}";
}

/// <summary>
/// Resultado imediato do reenvio.
/// </summary>
/// <param name="StatusCode">Status HTTP devolvido pela sua URL; <see langword="null"/> quando ela não respondeu (<c>status_http</c>).</param>
/// <param name="Response">Corpo devolvido pela sua URL (<c>resposta</c>).</param>
public sealed record WebhookResendResult(int? StatusCode, string Response)
{
    /// <summary>
    /// Nunca inclui o corpo da resposta.
    /// </summary>
    public override string ToString() => $"WebhookResendResult {{ StatusCode = {StatusCode}, Response = *** }}";
}
