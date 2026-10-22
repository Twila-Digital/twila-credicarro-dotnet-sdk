namespace Credicarro.Proposals.Models;

/// <summary>
/// Participante da proposta. No envio de documento vira <c>participante</c>: <c>COMPRADOR</c>, <c>VENDEDOR</c> ou <c>LOJA</c>.
/// </summary>
public enum ProposalParticipant
{
    Buyer = 1,
    Seller = 2,
    Store = 3
}
