using Credicarro.Serialization;

namespace Credicarro.Webhooks.Models;

/// <summary>
/// Evento notificado pelo webhook (<c>tipo</c>). Hoje só <see cref="Proposal"/> envia notificações.
/// </summary>
public enum WebhookType
{
    Customer = 1,
    Simulation = 2,
    Proposal = 3,

    [UnknownValue]
    Unknown = -1
}

/// <summary>
/// Autenticação enviada pela Credicarro ao chamar a URL (<c>tipo_autenticacao</c>). Não pode ser alterada depois do cadastro.
/// </summary>
public enum WebhookAuthenticationType
{
    /// <summary>Sem autenticação (não recomendado).</summary>
    None = 1,

    /// <summary><c>Authorization: Basic {credencial}</c>, com <c>usuario:senha</c> em Base64.</summary>
    Basic = 2,

    /// <summary><c>Authorization: Bearer {credencial}</c>.</summary>
    Jwt = 3,

    [UnknownValue]
    Unknown = -1
}

/// <summary>
/// Status do webhook (<c>status</c>).
/// </summary>
public enum WebhookStatus
{
    Active = 1,
    Inactive = 2,

    [UnknownValue]
    Unknown = -1
}

/// <summary>
/// O que disparou a notificação do webhook detalhado (<c>motivo_evento</c>).
/// </summary>
public enum ProposalEventReason
{
    /// <summary><c>STATUS_ALTERADO</c></summary>
    StatusChanged = 1,

    /// <summary><c>ETAPAS_ALTERADAS</c></summary>
    StepsChanged = 2,

    /// <summary><c>DOCUMENTOS_ALTERADOS</c></summary>
    DocumentsChanged = 3,

    [UnknownValue]
    Unknown = -1
}
