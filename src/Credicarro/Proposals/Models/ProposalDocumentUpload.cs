using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Documento de um participante da proposta (<c>POST v2/proposal/{id}/document</c>), um arquivo por chamada.
/// </summary>
/// <param name="Participant">Participante dono do documento (<c>participante</c>).</param>
/// <param name="DocumentType">Tipo do documento (<c>tipoDocumento</c>); confira os tipos aceitos por participante na documentação.</param>
/// <param name="File">Arquivo (<c>nomeArquivo</c> e <c>conteudoBase64</c>).</param>
public sealed record ProposalDocumentUpload(ProposalParticipant Participant, DocumentType DocumentType, DocumentFile File)
{
    public override string ToString() =>
        $"ProposalDocumentUpload {{ Participant = {Participant}, DocumentType = {DocumentType}, File = {File} }}";
}
