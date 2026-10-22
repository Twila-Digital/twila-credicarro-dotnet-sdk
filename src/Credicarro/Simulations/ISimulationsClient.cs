using Credicarro.Simulations.Models;

namespace Credicarro.Simulations;

/// <summary>
/// Simulações de financiamento e refinanciamento (rotas v2, que selecionam a loja pelo CNPJ) e consultas de simulações (v1).
/// O cliente precisa estar cadastrado antes de simular.
/// </summary>
public interface ISimulationsClient
{
    /// <summary>Valor mínimo de entrada por prazo (<c>GET v2/proposal/simulation/range/financing</c>).</summary>
    Task<IReadOnlyList<FinancingRangeOption>> GetFinancingRangeAsync(string licensePlate, string storeCnpj, CancellationToken cancellationToken = default);

    /// <summary>Faixa de valor refinanciável por prazo (<c>GET v2/proposal/simulation/range/refinancing</c>).</summary>
    Task<IReadOnlyList<RefinancingRangeOption>> GetRefinancingRangeAsync(string licensePlate, string storeCnpj, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retornos de comissão configurados para a loja, por tipo de simulação (<c>GET v2/proposal/simulation/commission-returns</c>).
    /// Use um dos valores retornados em <c>CommissionReturn</c> ao criar a simulação.
    /// </summary>
    Task<IReadOnlyList<CommissionReturn>> GetCommissionReturnsAsync(string storeCnpj, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simula um financiamento (<c>POST v2/proposal/simulation/financing</c>); devolve uma opção por prazo.
    /// A <c>Idempotency-Key</c> é automática e repetida em todas as tentativas.
    /// </summary>
    Task<IReadOnlyList<SimulationOption>> CreateFinancingAsync(CreateFinancingSimulationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simula um refinanciamento (<c>POST v2/proposal/simulation/refinancing</c>); devolve uma opção por prazo.
    /// A <c>Idempotency-Key</c> é automática e repetida em todas as tentativas.
    /// </summary>
    Task<IReadOnlyList<SimulationOption>> CreateRefinancingAsync(CreateRefinancingSimulationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Consulta uma simulação pelo identificador (<c>GET v1/proposal/simulation/{id}</c>).</summary>
    Task<Simulation> GetAsync(Guid simulationId, CancellationToken cancellationToken = default);

    /// <summary>Lista as simulações do parceiro (<c>GET v1/proposal/simulation</c>).</summary>
    Task<PagedResult<Simulation>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default);
}
