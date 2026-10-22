using Credicarro.Customers;
using Credicarro.Proposals;
using Credicarro.Simulations;
using Credicarro.V1;
using Credicarro.Webhooks;

namespace Credicarro;

/// <summary>
/// Ponto de entrada do SDK do Credicarro. Registre-o com <c>services.AddCredicarro(...)</c> e injete-o como singleton.
/// </summary>
public interface ICredicarroClient
{
    /// <summary>Cadastro do cliente (comprador).</summary>
    ICustomersClient Customers { get; }

    /// <summary>Simulações de financiamento e refinanciamento (v2) e consultas de simulações.</summary>
    ISimulationsClient Simulations { get; }

    /// <summary>Propostas, dados do veículo, conta de recebimento e envio de documentos (v2).</summary>
    IProposalsClient Proposals { get; }

    /// <summary>Cadastro e gerenciamento de webhooks.</summary>
    IWebhooksClient Webhooks { get; }

    /// <summary>Rotas da versão 1 que não usam <c>cnpjLoja</c>.</summary>
    IV1Client V1 { get; }
}
