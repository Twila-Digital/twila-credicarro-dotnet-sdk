using System.Net;
using System.Net.Http.Headers;
using Credicarro.Errors;
using Credicarro.Http;

namespace Credicarro.Authentication;

/// <summary>
/// Anexa o Bearer token. Em <c>401</c> renova o token e repete a chamada <b>uma</b> vez; um segundo <c>401</c> vira
/// <see cref="CredicarroAuthenticationException"/>. A primeira tentativa usa a própria requisição; só o replay do 401
/// cria um clone, que compartilha o corpo já materializado (sem copiar o payload).
/// </summary>
internal sealed class AuthenticationDelegatingHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await request.EnsureBufferedAsync().ConfigureAwait(false);

        var token = await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        tokenProvider.Invalidate(token);
        response.Dispose();

        var secondAttempt = request.Clone();
        var newToken = await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        secondAttempt.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        var secondResponse = await base.SendAsync(secondAttempt, cancellationToken).ConfigureAwait(false);

        if (secondResponse.StatusCode != HttpStatusCode.Unauthorized)
            return secondResponse;

        using (secondResponse)
        {
            throw await CredicarroExceptionFactory.FromResponseAsync(secondResponse, cancellationToken).ConfigureAwait(false);
        }
    }
}
