using Credicarro.Proposals.Models;

namespace Credicarro.Webhooks.Models;

/// <summary>
/// Notificação de proposta. Existem dois formatos, ambos aceitos:
/// o simples (<c>id_proposta</c>, <c>enum_status</c> e <c>status</c> em inglês) e o detalhado, de propostas criadas por
/// <c>POST v2/proposal</c>, com o retrato completo (<see cref="IsDetailed"/>).
/// </summary>
/// <param name="ProposalId">Identificador da proposta (<c>id_proposta</c>).</param>
/// <param name="Status">Status da proposta.</param>
/// <param name="StatusRaw">Código bruto do status (<c>enum_status</c>).</param>
/// <param name="StatusName">Nome do status como veio no payload (<c>APROVADA</c> no detalhado, <c>Approved</c> no simples).</param>
/// <param name="EventReason">O que disparou a notificação (<c>motivo_evento</c>), só no detalhado.</param>
/// <param name="EventReasonRaw">Texto bruto de <c>motivo_evento</c>.</param>
/// <param name="RefusalReason">Motivo da recusa (<c>motivo_recusa</c>), quando houver.</param>
/// <param name="EventDate">Momento em que o retrato foi montado (<c>data_evento</c>), só no detalhado.</param>
/// <param name="Buyer">Comprador (<c>comprador</c>), só no detalhado.</param>
/// <param name="Seller">Vendedor (<c>vendedor</c>); hoje sempre ausente.</param>
/// <param name="Store">Loja (<c>loja</c>), só no detalhado e quando a proposta foi feita em loja.</param>
/// <param name="IsDetailed"><see langword="true"/> para o payload detalhado.</param>
public sealed record ProposalWebhookEvent(
    Guid ProposalId,
    ProposalStatus Status,
    int StatusRaw,
    string? StatusName,
    ProposalEventReason? EventReason,
    string? EventReasonRaw,
    string? RefusalReason,
    DateTimeOffset? EventDate,
    ProposalParticipantState? Buyer,
    ProposalParticipantState? Seller,
    ProposalParticipantState? Store,
    bool IsDetailed)
{
    /// <summary>
    /// O que o seu sistema precisa enviar, a partir do retrato do evento detalhado (mesma regra de <c>ProposalDetails.GetPendingActions</c>).
    /// O evento simples não traz etapas: devolve lista vazia, e então consulte a proposta.
    /// </summary>
    public IReadOnlyList<PendingAction> GetPendingActions() => PendingActionResolver.Resolve(Buyer, Seller, Store);

    /// <summary>Sem motivo da recusa nem CPFs.</summary>
    public override string ToString() =>
        $"ProposalWebhookEvent {{ ProposalId = {ProposalId}, Status = {Status}, EventReason = {EventReason}, EventDate = {EventDate:o}, IsDetailed = {IsDetailed}, Buyer = {Buyer}, Seller = {Seller}, Store = {Store}, RefusalReason = {Credicarro.Internal.Masking.Hidden} }}";
}
