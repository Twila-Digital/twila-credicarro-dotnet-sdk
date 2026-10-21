using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Retrato atual da proposta (<c>GET v2/proposal/{id}</c>): status, link de biometria e as etapas e os documentos do
/// comprador, da loja e, quando houver, do vendedor.
/// </summary>
/// <param name="Id">Identificador da proposta (<c>id_proposta</c>).</param>
/// <param name="Number">Número único da proposta (<c>numero</c>).</param>
/// <param name="SimulationId">Identificador da simulação (<c>id_simulacao</c>).</param>
/// <param name="Status">Status da proposta (<c>enum_status</c>).</param>
/// <param name="StatusRaw">Código bruto do status (<c>enum_status</c>).</param>
/// <param name="StatusName">Nome do status (<c>status</c>), por exemplo <c>APROVADA</c>.</param>
/// <param name="Type">Modalidade (<c>tipo</c>).</param>
/// <param name="CreatedAt">Data de criação (<c>data_criacao</c>).</param>
/// <param name="RefusalReason">Motivo da recusa (<c>motivo_recusa</c>), quando houver.</param>
/// <param name="BiometryLink">Link para o cliente fazer a biometria (<c>link_biometria</c>), quando disponível.</param>
/// <param name="Buyer">Comprador (<c>comprador</c>).</param>
/// <param name="Seller">Vendedor (<c>vendedor</c>), quando houver.</param>
/// <param name="Store">Loja de integração (<c>loja</c>), quando a proposta foi feita em loja.</param>
public sealed record ProposalDetails(
    Guid Id,
    long Number,
    Guid SimulationId,
    ProposalStatus Status,
    int StatusRaw,
    string? StatusName,
    ProposalType Type,
    DateTimeOffset? CreatedAt,
    string? RefusalReason,
    string? BiometryLink,
    ProposalParticipantState? Buyer,
    ProposalParticipantState? Seller,
    ProposalParticipantState? Store)
{
    /// <summary>
    /// Lista o que o seu sistema precisa enviar agora: só os participantes cuja etapa ativa tem
    /// <see cref="ActionOwner.Integrator"/> como responsável. Etapa 6 gera <see cref="PendingActionKind.SendDocuments"/>
    /// (documentos com status <see cref="DocumentStatus.Pending"/>); 7, <see cref="PendingActionKind.CorrectDocuments"/>
    /// (<see cref="DocumentStatus.Invalid"/>); 8, <see cref="PendingActionKind.SendVehicleDetails"/>; 5,
    /// <see cref="PendingActionKind.SendDepositAccount"/>.
    /// </summary>
    public IReadOnlyList<PendingAction> GetPendingActions() => PendingActionResolver.Resolve(Buyer, Seller, Store);

    /// <summary>Sem CPF, motivo da recusa nem link de biometria.</summary>
    public override string ToString() =>
        $"ProposalDetails {{ Id = {Id}, Number = {Number}, Status = {Status}, Type = {Type}, CreatedAt = {CreatedAt:o}, " +
        $"RefusalReason = {Masking.Hidden}, BiometryLink = {Masking.Hidden}, Buyer = {Buyer}, Seller = {Seller}, Store = {Store} }}";
}

/// <summary>
/// Regra única de "o que cabe ao integrador", usada pela consulta da proposta e pelo webhook detalhado.
/// </summary>
internal static class PendingActionResolver
{
    public static IReadOnlyList<PendingAction> Resolve(ProposalParticipantState? buyer, ProposalParticipantState? seller, ProposalParticipantState? store)
    {
        var actions = new List<PendingAction>();

        Collect(actions, ProposalParticipant.Buyer, buyer);
        Collect(actions, ProposalParticipant.Seller, seller);
        Collect(actions, ProposalParticipant.Store, store);

        return actions;
    }

    private static void Collect(List<PendingAction> actions, ProposalParticipant participant, ProposalParticipantState? state)
    {
        var active = state?.ActiveStep;

        if (state is null || active is null || active.ActionOwner != ActionOwner.Integrator)
            return;

        switch (active.Step)
        {
            case ProposalStep.DocumentSubmission:
                actions.Add(new PendingAction(participant, active.Step, PendingActionKind.SendDocuments, DocumentTypesWith(state, DocumentStatus.Pending)));
                break;
            case ProposalStep.DocumentCorrection:
                actions.Add(new PendingAction(participant, active.Step, PendingActionKind.CorrectDocuments, DocumentTypesWith(state, DocumentStatus.Invalid)));
                break;
            case ProposalStep.VehicleDetails:
                actions.Add(new PendingAction(participant, active.Step, PendingActionKind.SendVehicleDetails, Array.Empty<DocumentType>()));
                break;
            case ProposalStep.DepositAccount:
                actions.Add(new PendingAction(participant, active.Step, PendingActionKind.SendDepositAccount, Array.Empty<DocumentType>()));
                break;
            default:
                break;
        }
    }

    private static IReadOnlyList<DocumentType> DocumentTypesWith(ProposalParticipantState state, DocumentStatus status)
    {
        var types = new List<DocumentType>();

        foreach (var document in state.Documents)
        {
            if (document.Status == status)
                types.Add(document.Type);
        }

        return types;
    }
}
