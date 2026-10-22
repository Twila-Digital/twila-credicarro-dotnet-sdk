using Credicarro.Http;
using Credicarro.Internal.Generated.Proposals;
using Credicarro.Internal.Generated.Simulations;
using Credicarro.Internal.Mapping;
using Credicarro.Proposals.Models;
using Credicarro.Simulations.Models;
using Credicarro.V1.Models;

namespace Credicarro.V1;

internal sealed class V1Client(IV1SimulationsClient simulations, IV1ProposalsClient proposals) : IV1Client
{
    public IV1SimulationsClient Simulations { get; } = simulations;

    public IV1ProposalsClient Proposals { get; } = proposals;
}

internal sealed class V1SimulationsClient(HttpClient httpClient) : IV1SimulationsClient
{
    public async Task<IReadOnlyList<FinancingRangeOption>> GetFinancingRangeAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var path = RangePath("v1/proposal/simulation/range/financing", licensePlate);

        var wire = await ApiTransport.GetAsync<FinancingRangeResponseWire>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return (wire.OpcoesPorPrazo ?? Array.Empty<FinancingRangeOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<RefinancingRangeOption>> GetRefinancingRangeAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var path = RangePath("v1/proposal/simulation/range/refinancing", licensePlate);

        var wire = await ApiTransport.GetAsync<RefinancingRangeResponseWire>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return (wire.OpcoesPorPrazo ?? Array.Empty<RefinancingRangeOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<SimulationOption>> CreateFinancingAsync(CreateFinancingSimulationV1Request request, CancellationToken cancellationToken = default)
    {
        var wireRequest = SimulationMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<SimulationListResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/proposal/simulation/financing", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return (wire.Simulacoes ?? Array.Empty<SimulationOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    public async Task<IReadOnlyList<SimulationOption>> CreateRefinancingAsync(CreateRefinancingSimulationV1Request request, CancellationToken cancellationToken = default)
    {
        var wireRequest = SimulationMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<SimulationListResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/proposal/simulation/refinancing", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return (wire.Simulacoes ?? Array.Empty<SimulationOptionWire>()).Select(SimulationMapper.ToPublic).ToList();
    }

    private static string RangePath(string route, string licensePlate)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            throw new ArgumentException("A placa é obrigatória.", nameof(licensePlate));

        return new QueryStringBuilder().Add("placa", licensePlate).Build(route);
    }
}

internal sealed class V1ProposalsClient(HttpClient httpClient) : IV1ProposalsClient
{
    public async Task<Guid> CreateAsync(CreateProposalRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = ProposalMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<ProposalIdResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/proposal", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return wire.IdProposta;
    }

    public async Task<ProposalSummary> GetAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        var wire = await ApiTransport
            .GetAsync<ProposalResponseWire>(httpClient, $"v1/proposal/{proposalId}", cancellationToken)
            .ConfigureAwait(false);

        return ProposalMapper.ToPublic(wire);
    }
}
