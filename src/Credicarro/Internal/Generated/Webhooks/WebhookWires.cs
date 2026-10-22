using System.Text.Json.Serialization;
using Credicarro.Internal.Generated.Proposals;

namespace Credicarro.Internal.Generated.Webhooks;

#pragma warning disable S3996

internal sealed record CreateWebhookRequestWire(
    [property: JsonPropertyName("tipo")] int Tipo,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("tipo_autenticacao")] int TipoAutenticacao,
    [property: JsonPropertyName("credencial")] string? Credencial);

internal sealed record CreateWebhookResponseWire(
    [property: JsonPropertyName("id_webhook")] Guid IdWebhook,
    [property: JsonPropertyName("segredo_assinatura")] string? SegredoAssinatura);

internal sealed record UpdateWebhookRequestWire(
    [property: JsonPropertyName("tipo")] int Tipo,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("status")] int? Status);

internal sealed record WebhookWire(
    [property: JsonPropertyName("id_webhook")] Guid IdWebhook,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("tipo_autenticacao")] string? TipoAutenticacao,
    [property: JsonPropertyName("status")] string? Status);

/// <summary>
/// Payload da notificação. O formato simples traz só <c>id_proposta</c>, <c>enum_status</c> e <c>status</c>;
/// o detalhado traz também o retrato (motivo, data, comprador, loja e vendedor).
/// </summary>
internal sealed record ProposalWebhookEventWire(
    [property: JsonPropertyName("id_proposta")] Guid? IdProposta,
    [property: JsonPropertyName("enum_status")] int? EnumStatus,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("motivo_evento")] string? MotivoEvento,
    [property: JsonPropertyName("motivo_recusa")] string? MotivoRecusa,
    [property: JsonPropertyName("data_evento")] DateTimeOffset? DataEvento,
    [property: JsonPropertyName("comprador")] ProposalParticipantWire? Comprador,
    [property: JsonPropertyName("loja")] ProposalStoreWire? Loja,
    [property: JsonPropertyName("vendedor")] ProposalParticipantWire? Vendedor);
