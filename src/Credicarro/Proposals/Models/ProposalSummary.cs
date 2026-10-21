using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Proposta resumida (<c>GET v1/proposal/{id}</c> e <c>GET v1/proposal</c>). Para etapas e documentos, use
/// <c>Proposals.GetAsync</c> (v2).
/// </summary>
/// <param name="Id">Identificador da proposta (<c>id_proposta</c>).</param>
/// <param name="Number">Número único da proposta (<c>numero</c>).</param>
/// <param name="SimulationId">Identificador da simulação (<c>id_simulacao</c>).</param>
/// <param name="Type">Modalidade (<c>tipo</c>).</param>
/// <param name="Status">Status (lido do nome em inglês, por exemplo <c>Approved</c>).</param>
/// <param name="StatusName">Nome do status como veio da API.</param>
/// <param name="BuyerCpf">CPF do comprador (<c>comprador.cpf</c>).</param>
/// <param name="SellerCpf">CPF do vendedor (<c>vendedor.cpf</c>), omitido quando a proposta não tem vendedor.</param>
/// <param name="CreatedAt">Data de criação (<c>data_criacao</c>).</param>
public sealed record ProposalSummary(
    Guid Id,
    long Number,
    Guid SimulationId,
    ProposalType Type,
    ProposalStatus Status,
    string? StatusName,
    string? BuyerCpf,
    string? SellerCpf,
    DateTimeOffset? CreatedAt)
{
    /// <summary>CPFs mascarados.</summary>
    public override string ToString() =>
        $"ProposalSummary {{ Id = {Id}, Number = {Number}, Type = {Type}, Status = {Status}, BuyerCpf = {Masking.Cpf(BuyerCpf)}, " +
        $"SellerCpf = {(SellerCpf is null ? "null" : Masking.Cpf(SellerCpf))}, CreatedAt = {CreatedAt:o} }}";
}
