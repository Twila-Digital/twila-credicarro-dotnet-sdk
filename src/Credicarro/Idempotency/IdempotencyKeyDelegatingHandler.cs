using Credicarro.Configuration;
using Credicarro.Http;

namespace Credicarro.Idempotency;

/// <summary>
/// Gera uma <c>Idempotency-Key</c> (UUID) por chamada lógica nas rotas de criação. Fica na ponta externa da pipeline,
/// antes do retry, então a mesma chave vai em todas as tentativas.
/// <para>
/// <c>POST v1/customer/unified</c> nunca recebe chave automática: com a chave a rota só cria (CPF existente devolve
/// <c>409 Cpf.CpfAlreadyAdded</c>); sem a chave ela cria ou atualiza. Chaves já presentes na requisição são preservadas.
/// </para>
/// </summary>
internal sealed class IdempotencyKeyDelegatingHandler(CredicarroOptions options) : DelegatingHandler
{
    internal const string HeaderName = "Idempotency-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!options.Resilience.DisableAutomaticIdempotencyKey
            && ShouldAttachIdempotencyKey(request)
            && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, Guid.NewGuid().ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool ShouldAttachIdempotencyKey(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post && ApiRoutes.RequiresAutomaticIdempotencyKey(ApiRoutes.GetApiPath(request));
}
