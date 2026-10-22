using Credicarro.Customers;
using Credicarro.Proposals;
using Credicarro.Simulations;
using Credicarro.V1;
using Credicarro.Webhooks;

namespace Credicarro;

internal sealed class CredicarroClient(
    ICustomersClient customers,
    ISimulationsClient simulations,
    IProposalsClient proposals,
    IWebhooksClient webhooks,
    IV1Client v1) : ICredicarroClient
{
    public ICustomersClient Customers { get; } = customers;

    public ISimulationsClient Simulations { get; } = simulations;

    public IProposalsClient Proposals { get; } = proposals;

    public IWebhooksClient Webhooks { get; } = webhooks;

    public IV1Client V1 { get; } = v1;
}
