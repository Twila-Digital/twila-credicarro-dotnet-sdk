using Credicarro.Webhooks.Models;

namespace Credicarro.Webhooks;

/// <summary>
/// Gerenciamento dos webhooks do parceiro. Os webhooks são específicos de cada ambiente.
/// </summary>
public interface IWebhooksClient
{
    /// <summary>
    /// Cadastra um webhook ativo (<c>POST v1/webhook</c>). Esta chamada <b>não é repetida automaticamente</b>, porque não
    /// tem <c>Idempotency-Key</c> e repetir cadastraria o webhook de novo. O <see cref="CreateWebhookResult.SigningSecret"/> é
    /// devolvido só nesta resposta: guarde-o.
    /// </summary>
    Task<CreateWebhookResult> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lista os webhooks do parceiro (<c>GET v1/webhook</c>).</summary>
    Task<PagedResult<Webhook>> ListAsync(ListRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edita tipo, URL e status (<c>PUT v1/webhook/{id}</c>, resposta <c>204</c>). A autenticação não muda: exclua e cadastre outro.
    /// </summary>
    Task UpdateAsync(Guid webhookId, UpdateWebhookRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exclui o webhook (<c>DELETE v1/webhook/{id}</c>, resposta <c>204</c>).</summary>
    Task DeleteAsync(Guid webhookId, CancellationToken cancellationToken = default);
}
