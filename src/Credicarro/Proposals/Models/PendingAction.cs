namespace Credicarro.Proposals.Models;

/// <summary>
/// O que o integrador precisa enviar para a proposta andar.
/// </summary>
public enum PendingActionKind
{
    /// <summary>Etapa <c>ENVIO_DOCUMENTOS</c> (6): enviar os documentos com status <c>PENDENTE</c>.</summary>
    SendDocuments = 1,

    /// <summary>Etapa <c>CORRECAO_DOCUMENTOS</c> (7): reenviar os documentos com status <c>INVALIDO</c> (com uma nova <c>Idempotency-Key</c>).</summary>
    CorrectDocuments = 2,

    /// <summary>Etapa <c>DADOS_VEICULO</c> (8): enviar os dados do veículo.</summary>
    SendVehicleDetails = 3,

    /// <summary>Etapa <c>CONTA_RECEBIMENTO</c> (5): enviar a conta de recebimento (refinanciamento).</summary>
    SendDepositAccount = 4
}

/// <summary>
/// Ação que cabe ao integrador (<c>ActionOwner == Integrator</c>) para um participante da proposta.
/// </summary>
/// <param name="Participant">Participante que está esperando.</param>
/// <param name="Step">Etapa ativa do participante.</param>
/// <param name="Kind">O que enviar.</param>
/// <param name="DocumentTypes">Tipos de documento a enviar (vazio quando <paramref name="Kind"/> não é de documentos).</param>
public sealed record PendingAction(
    ProposalParticipant Participant,
    ProposalStep Step,
    PendingActionKind Kind,
    IReadOnlyList<DocumentType> DocumentTypes);
