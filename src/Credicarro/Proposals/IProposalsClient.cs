using Credicarro.Proposals.Models;

namespace Credicarro.Proposals;

/// <summary>
/// Propostas de financiamento e refinanciamento (rotas v2, que acompanham etapas e documentos) e a listagem v1.
/// </summary>
public interface IProposalsClient
{
    /// <summary>
    /// Cria a proposta a partir de uma opção de simulação (<c>POST v2/proposal</c>). A simulação precisa ter sido feita com
    /// <c>cnpjLoja</c>, e o cliente precisa ter o cadastro completo (endereço, ocupação e renda).
    /// A <c>Idempotency-Key</c> é automática e repetida em todas as tentativas.
    /// </summary>
    /// <returns>O identificador da proposta (<c>id_proposta</c>).</returns>
    Task<Guid> CreateAsync(CreateProposalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta o retrato atual da proposta (<c>GET v2/proposal/{id}</c>). Use
    /// <see cref="ProposalDetails.GetPendingActions"/> para saber o que o seu sistema precisa enviar.
    /// </summary>
    Task<ProposalDetails> GetAsync(Guid proposalId, CancellationToken cancellationToken = default);

    /// <summary>Lista as propostas do parceiro, em formato resumido (<c>GET v1/proposal</c>).</summary>
    Task<PagedResult<ProposalSummary>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia os dados do veículo (<c>POST v2/proposal/{id}/vehicle-detail</c>, resposta <c>204</c>), aceitos enquanto a
    /// etapa <c>DADOS_VEICULO</c> estiver ativa. Reenviar substitui os dados, então o SDK repete a chamada em falhas transitórias.
    /// </summary>
    Task SendVehicleDetailsAsync(Guid proposalId, VehicleDetails details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia a conta de recebimento de uma proposta de refinanciamento (<c>POST v2/proposal/{id}/deposit-account</c>, resposta
    /// <c>204</c>). Reenviar substitui a conta, então o SDK repete a chamada em falhas transitórias.
    /// </summary>
    Task SendDepositAccountAsync(Guid proposalId, DepositAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um documento de um participante (<c>POST v2/proposal/{id}/document</c>, resposta <c>204</c>), um arquivo por chamada.
    /// <para>
    /// O header <c>Idempotency-Key</c> é obrigatório: gere a chave <b>uma vez por envio</b>, guarde-a junto ao registro do
    /// documento no seu sistema e passe a mesma em <paramref name="idempotencyKey"/> ao repetir esse envio (com a mesma
    /// chave e o mesmo conteúdo, um envio bem-sucedido nas últimas 24 horas não é processado de novo). Use uma chave
    /// <b>nova</b> ao reenviar uma correção, porque uma chave nova substitui o documento.
    /// </para>
    /// <para>
    /// Sem <paramref name="idempotencyKey"/>, o SDK gera uma chave por chamada e a repete em todas as tentativas automáticas
    /// dessa chamada (retry de rede, <c>429</c>, <c>500</c>, <c>502</c>, <c>503</c>, <c>504</c>).
    /// </para>
    /// </summary>
    Task UploadDocumentAsync(Guid proposalId, ProposalDocumentUpload upload, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
