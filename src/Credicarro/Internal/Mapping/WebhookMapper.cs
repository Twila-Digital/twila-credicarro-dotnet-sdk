using Credicarro.Http;
using Credicarro.Internal.Generated.Webhooks;
using Credicarro.Proposals.Models;
using Credicarro.Serialization;
using Credicarro.Webhooks.Models;

namespace Credicarro.Internal.Mapping;

internal static class WebhookMapper
{
    public static CreateWebhookRequestWire ToWire(CreateWebhookRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (!EnumMapping.IsKnown(request.Type))
            throw new ArgumentException("O tipo de webhook informado não é válido.", nameof(request));

        if (!EnumMapping.IsKnown(request.AuthenticationType))
            throw new ArgumentException("O tipo de autenticação informado não é válido.", nameof(request));

        if (request.AuthenticationType != WebhookAuthenticationType.None && string.IsNullOrWhiteSpace(request.Credential))
            throw new ArgumentException("A credencial é obrigatória para a autenticação Basic ou Jwt.", nameof(request));

        return new CreateWebhookRequestWire(
            (int)request.Type,
            request.Url,
            (int)request.AuthenticationType,
            request.Credential);
    }

    public static UpdateWebhookRequestWire ToWire(UpdateWebhookRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (!EnumMapping.IsKnown(request.Type))
            throw new ArgumentException("O tipo de webhook informado não é válido.", nameof(request));

        if (request.Status is { } status && !EnumMapping.IsKnown(status))
            throw new ArgumentException("O status de webhook informado não é válido.", nameof(request));

        return new UpdateWebhookRequestWire((int)request.Type, request.Url, request.Status is { } value ? (int)value : (int?)null);
    }

    public static Webhook ToPublic(WebhookWire wire) => new(
        wire.IdWebhook,
        EnumMapping.FromWireName<WebhookType>(wire.Tipo),
        wire.Url ?? string.Empty,
        EnumMapping.FromWireName<WebhookAuthenticationType>(wire.TipoAutenticacao),
        EnumMapping.FromWireName<WebhookStatus>(wire.Status));

    public static WebhookDelivery ToPublic(WebhookDeliveryWire wire) => new(
        wire.IdAuditoria,
        wire.IdWebhook,
        EnumMapping.FromWireName<WebhookType>(wire.Tipo),
        wire.Requisicao ?? string.Empty,
        wire.Resposta ?? string.Empty,
        wire.StatusHttp,
        wire.DataCriacao);

    public static WebhookDeliveryChartPoint ToPublic(WebhookDeliveryChartPointWire wire) =>
        new(wire.Rotulo ?? string.Empty, wire.Sucessos, wire.Erros);

    public static WebhookResendResult ToPublic(WebhookResendResponseWire wire) =>
        new(wire.StatusHttp, wire.Resposta ?? string.Empty);

    public static QueryStringBuilder AddFilter(QueryStringBuilder query, WebhookDeliveryFilter? filter) =>
        filter is null
            ? query
            : query
                .Add("data_inicial", filter.InitialDate)
                .Add("data_final", filter.FinalDate)
                .Add("id_proposta", filter.ProposalId)
                .Add("id_simulacao", filter.SimulationId)
                .Add("numero_proposta", filter.ProposalNumber)
                .Add("status_http", filter.StatusCode);

    public static ProposalWebhookEvent ToPublic(ProposalWebhookEventWire wire)
    {
        var status = ProposalMapper.ParseStatus(wire.EnumStatus, wire.Status);
        var isDetailed = wire.MotivoEvento is not null
            || wire.DataEvento is not null
            || wire.Comprador is not null
            || wire.Loja is not null
            || wire.Vendedor is not null;

        return new ProposalWebhookEvent(
            wire.IdProposta!.Value,
            status,
            wire.EnumStatus ?? (status == ProposalStatus.Unknown ? -1 : (int)status),
            wire.Status,
            ParseEventReason(wire.MotivoEvento),
            wire.MotivoEvento,
            wire.MotivoRecusa,
            wire.DataEvento,
            ProposalMapper.ToPublic(wire.Comprador),
            ProposalMapper.ToPublic(wire.Vendedor),
            ProposalMapper.ToPublic(wire.Loja),
            isDetailed);
    }

    private static ProposalEventReason? ParseEventReason(string? reason) => reason switch
    {
        null => null,
        "STATUS_ALTERADO" => ProposalEventReason.StatusChanged,
        "ETAPAS_ALTERADAS" => ProposalEventReason.StepsChanged,
        "DOCUMENTOS_ALTERADOS" => ProposalEventReason.DocumentsChanged,
        _ => ProposalEventReason.Unknown
    };
}
