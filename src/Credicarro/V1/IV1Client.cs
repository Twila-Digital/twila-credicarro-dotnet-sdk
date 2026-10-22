using Credicarro.V1.Models;
using Credicarro.Proposals.Models;
using Credicarro.Simulations.Models;

namespace Credicarro.V1;

/// <summary>
/// Rotas da versão 1 que não selecionam a loja pelo CNPJ (<c>cnpjLoja</c>). Para novas integrações, prefira
/// <c>client.Simulations</c> e <c>client.Proposals</c> (v2), que acompanham etapas e documentos.
/// </summary>
public interface IV1Client
{
    IV1SimulationsClient Simulations { get; }

    IV1ProposalsClient Proposals { get; }
}

/// <summary>
/// Simulações da versão 1: a loja de integração é a única vinculada ao parceiro.
/// </summary>
public interface IV1SimulationsClient
{
    /// <summary>Valor mínimo de entrada por prazo (<c>GET v1/proposal/simulation/range/financing</c>).</summary>
    Task<IReadOnlyList<FinancingRangeOption>> GetFinancingRangeAsync(string licensePlate, CancellationToken cancellationToken = default);

    /// <summary>Faixa de valor refinanciável por prazo (<c>GET v1/proposal/simulation/range/refinancing</c>).</summary>
    Task<IReadOnlyList<RefinancingRangeOption>> GetRefinancingRangeAsync(string licensePlate, CancellationToken cancellationToken = default);

    /// <summary>Simula um financiamento (<c>POST v1/proposal/simulation/financing</c>). A <c>Idempotency-Key</c> é automática.</summary>
    Task<IReadOnlyList<SimulationOption>> CreateFinancingAsync(CreateFinancingSimulationV1Request request, CancellationToken cancellationToken = default);

    /// <summary>Simula um refinanciamento (<c>POST v1/proposal/simulation/refinancing</c>). A <c>Idempotency-Key</c> é automática.</summary>
    Task<IReadOnlyList<SimulationOption>> CreateRefinancingAsync(CreateRefinancingSimulationV1Request request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Propostas da versão 1.
/// </summary>
public interface IV1ProposalsClient
{
    /// <summary>Cria a proposta (<c>POST v1/proposal</c>). A <c>Idempotency-Key</c> é automática.</summary>
    Task<Guid> CreateAsync(CreateProposalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Consulta a proposta em formato resumido (<c>GET v1/proposal/{id}</c>).</summary>
    Task<ProposalSummary> GetAsync(Guid proposalId, CancellationToken cancellationToken = default);
}
