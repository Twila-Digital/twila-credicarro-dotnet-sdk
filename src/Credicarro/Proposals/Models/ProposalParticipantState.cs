using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Retrato de um participante da proposta (comprador, vendedor ou loja), com etapa ativa, histórico e documentos.
/// É o mesmo tipo na consulta (<c>GET v2/proposal/{id}</c>) e no webhook detalhado.
/// </summary>
/// <param name="Document">CPF (comprador e vendedor) ou CNPJ (loja).</param>
/// <param name="Biometry">Situação da biometria; a loja não tem biometria.</param>
/// <param name="BiometryRaw">Código bruto da biometria (<c>enum_biometria</c>).</param>
/// <param name="BiometryName">Nome da biometria (<c>biometria</c>).</param>
/// <param name="ActiveStep">Etapa em que o participante está agora (<c>status_ativo</c>).</param>
/// <param name="CompletedSteps">Histórico de etapas concluídas (<c>etapas_concluidas</c>), sem a etapa ativa.</param>
/// <param name="Documents">Documentos do participante (<c>documentos</c>).</param>
public sealed record ProposalParticipantState(
    string? Document,
    BiometryStatus? Biometry,
    int? BiometryRaw,
    string? BiometryName,
    ActiveStep? ActiveStep,
    IReadOnlyList<CompletedStep> CompletedSteps,
    IReadOnlyList<ProposalDocument> Documents)
{
    /// <summary>Documento mascarado (CPF <c>***.456.789-**</c>).</summary>
    public override string ToString() =>
        $"ProposalParticipantState {{ Document = {Masking.Document(Document)}, Biometry = {Biometry}, ActiveStep = {ActiveStep}, " +
        $"CompletedSteps = {CompletedSteps.Count}, Documents = {Documents.Count} }}";
}

/// <summary>
/// Etapa ativa do participante: a etapa, a situação dela e quem precisa agir. Cada valor vem com o código bruto e o nome.
/// </summary>
public sealed record ActiveStep(
    ProposalStep Step,
    int StepRaw,
    string? StepName,
    StepSituation Situation,
    int SituationRaw,
    string? SituationName,
    ActionOwner ActionOwner,
    int ActionOwnerRaw,
    string? ActionOwnerName);

/// <summary>
/// Etapa já concluída, com a data da última passagem por ela.
/// </summary>
public sealed record CompletedStep(ProposalStep Step, int StepRaw, string? StepName, DateTimeOffset? Date);

/// <summary>
/// Documento de um participante, com tipo e status (código bruto e nome) e o motivo da recusa, quando houver.
/// </summary>
public sealed record ProposalDocument(
    DocumentType Type,
    int TypeRaw,
    string? TypeName,
    DocumentStatus Status,
    int StatusRaw,
    string? StatusName,
    string? Reason)
{
    /// <summary>Sem o motivo da recusa (texto livre que pode citar dados do cliente).</summary>
    public override string ToString() =>
        $"ProposalDocument {{ Type = {Type}, Status = {Status}, Reason = {Masking.Hidden} }}";
}
