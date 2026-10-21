using Credicarro.Http;
using Credicarro.Internal.Generated;
using Credicarro.Internal.Generated.Simulations;
using Credicarro.Internal.Mapping;
using Credicarro.Simulations.Models;

namespace Credicarro.Simulations;

internal sealed class SimulationsClient(HttpClient httpClient) : ISimulationsClient
{
    public async Task<IReadOnlyList<FinancingRangeOption>> GetFinancingRangeAsync(string licensePlate, string storeCnpj, CancellationToken cancellationToken = default)
    {
        var path = RangePath("v2/proposal/simulation/range/financing", licensePlate, storeCnpj);

        var wire = await ApiTransport.GetAsync<FinancingRangeResponseWire>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return (wire.OpcoesPorPrazo ?? Array.Empty<FinancingRangeOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<RefinancingRangeOption>> GetRefinancingRangeAsync(string licensePlate, string storeCnpj, CancellationToken cancellationToken = default)
    {
        var path = RangePath("v2/proposal/simulation/range/refinancing", licensePlate, storeCnpj);

        var wire = await ApiTransport.GetAsync<RefinancingRangeResponseWire>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return (wire.OpcoesPorPrazo ?? Array.Empty<RefinancingRangeOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<SimulationOption>> CreateFinancingAsync(CreateFinancingSimulationRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = SimulationMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<SimulationListResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v2/proposal/simulation/financing", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return (wire.Simulacoes ?? Array.Empty<SimulationOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<SimulationOption>> CreateRefinancingAsync(CreateRefinancingSimulationRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = SimulationMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<SimulationListResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v2/proposal/simulation/refinancing", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return (wire.Simulacoes ?? Array.Empty<SimulationOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<Simulation> GetAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        var wire = await ApiTransport
            .GetAsync<SimulationWire>(httpClient, $"v1/proposal/simulation/{simulationId}", cancellationToken)
            .ConfigureAwait(false);

        return SimulationMapper.ToPublic(wire);
    }

    public async Task<PagedResult<Simulation>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var path = (request ?? new ListRequest()).ToPath("v1/proposal/simulation");

        var wire = await ApiTransport.GetAsync<PagedResultWire<SimulationWire>>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return PagedResultMapper.ToPublic(wire, SimulationMapper.ToPublic);
    }

    private static string RangePath(string route, string licensePlate, string storeCnpj)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            throw new ArgumentException("A placa é obrigatória.", nameof(licensePlate));

        if (string.IsNullOrWhiteSpace(storeCnpj))
            throw new ArgumentException("O CNPJ da loja é obrigatório.", nameof(storeCnpj));

        return new QueryStringBuilder()
            .Add("placa", licensePlate)
            .Add("cnpjLoja", storeCnpj)
            .Build(route);
    }
}
