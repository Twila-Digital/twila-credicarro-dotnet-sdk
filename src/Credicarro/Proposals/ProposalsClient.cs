using Credicarro.Http;
using Credicarro.Internal.Generated;
using Credicarro.Internal.Generated.Proposals;
using Credicarro.Internal.Mapping;
using Credicarro.Proposals.Models;

namespace Credicarro.Proposals;

internal sealed class ProposalsClient(HttpClient httpClient) : IProposalsClient
{
    public async Task<Guid> CreateAsync(CreateProposalRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = ProposalMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<ProposalIdResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v2/proposal", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return wire.IdProposta;
    }

    public async Task<ProposalDetails> GetAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        var wire = await ApiTransport
            .GetAsync<ProposalV2ResponseWire>(httpClient, $"v2/proposal/{proposalId}", cancellationToken)
            .ConfigureAwait(false);

        return ProposalMapper.ToPublic(wire);
    }

    public async Task<PagedResult<ProposalSummary>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var path = (request ?? new ListRequest()).ToPath("v1/proposal");

        var wire = await ApiTransport.GetAsync<PagedResultWire<ProposalResponseWire>>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return PagedResultMapper.ToPublic(wire, ProposalMapper.ToPublic);
    }

    public Task SendVehicleDetailsAsync(Guid proposalId, VehicleDetails details, CancellationToken cancellationToken = default)
    {
        var wireRequest = ProposalMapper.ToWire(details);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Post, $"v2/proposal/{proposalId}/vehicle-detail", wireRequest),
            cancellationToken);
    }

    public Task SendDepositAccountAsync(Guid proposalId, DepositAccount account, CancellationToken cancellationToken = default)
    {
        var wireRequest = ProposalMapper.ToWire(account);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Post, $"v2/proposal/{proposalId}/deposit-account", wireRequest),
            cancellationToken);
    }

    public Task UploadDocumentAsync(Guid proposalId, ProposalDocumentUpload upload, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        // O header é obrigatório nesta rota: sem chave do integrador, geramos uma por chamada. Ela vai no request,
        // antes do retry, então é a mesma em todas as tentativas (mesmo com DisableAutomaticIdempotencyKey).
        var key = ApiTransport.ValidateIdempotencyKey(idempotencyKey, nameof(idempotencyKey)) ?? Guid.NewGuid().ToString();
        var wireRequest = ProposalMapper.ToWire(upload);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Post, $"v2/proposal/{proposalId}/document", wireRequest, key),
            cancellationToken);
    }
}
