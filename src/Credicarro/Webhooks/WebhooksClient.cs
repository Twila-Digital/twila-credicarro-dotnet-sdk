using Credicarro.Http;
using Credicarro.Internal.Generated;
using Credicarro.Internal.Generated.Webhooks;
using Credicarro.Internal.Mapping;
using Credicarro.Webhooks.Models;

namespace Credicarro.Webhooks;

internal sealed class WebhooksClient(HttpClient httpClient) : IWebhooksClient
{
    public async Task<CreateWebhookResult> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = WebhookMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<CreateWebhookResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/webhook", wireRequest),
                cancellationToken)
            .ConfigureAwait(false);

        return new CreateWebhookResult(wire.IdWebhook, wire.SegredoAssinatura ?? string.Empty);
    }

    public async Task<PagedResult<Webhook>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var path = (request ?? new ListRequest()).ToPath("v1/webhook");

        var wire = await ApiTransport.GetAsync<PagedResultWire<WebhookWire>>(httpClient, path, cancellationToken).ConfigureAwait(false);

        return PagedResultMapper.ToPublic(wire, WebhookMapper.ToPublic);
    }

    public Task UpdateAsync(Guid webhookId, UpdateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var wireRequest = WebhookMapper.ToWire(request);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Put, $"v1/webhook/{webhookId}", wireRequest),
            cancellationToken);
    }

    public Task DeleteAsync(Guid webhookId, CancellationToken cancellationToken = default) =>
        ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateRequest(HttpMethod.Delete, $"v1/webhook/{webhookId}"),
            cancellationToken);
}
