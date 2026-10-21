using Credicarro.Http;
using Credicarro.Idempotency;

namespace Credicarro.Resilience;

/// <summary>
/// Decide se uma requisição pode ser repetida sem efeito colateral duplicado.
/// <list type="bullet">
/// <item><c>GET</c>/<c>HEAD</c>/<c>OPTIONS</c>;</item>
/// <item><c>POST v1/authentication/accesstoken</c>;</item>
/// <item><c>POST</c> com <c>Idempotency-Key</c> (inclusive <c>v1/customer/unified</c> quando o integrador passa a chave);</item>
/// <item><c>POST v2/proposal/{id}/vehicle-detail</c> e <c>deposit-account</c> (reenviar substitui);</item>
/// <item><c>PUT</c>/<c>DELETE v1/webhook/{id}</c>.</item>
/// </list>
/// Não são seguras: <c>POST v1/webhook</c> e <c>POST v1/customer/unified</c> sem chave.
/// </summary>
internal static class IdempotencyClassifier
{
    public static bool IsRetrySafe(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.Method == HttpMethod.Options)
            return true;

        var path = ApiRoutes.GetApiPath(request);

        if (request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
            return ApiRoutes.IsWebhookResource(path);

        if (request.Method != HttpMethod.Post)
            return false;

        if (ApiRoutes.IsAuthentication(path) || ApiRoutes.IsReplaceSemantics(path))
            return true;

        return request.Headers.Contains(IdempotencyKeyDelegatingHandler.HeaderName);
    }
}
