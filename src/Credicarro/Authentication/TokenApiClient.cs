using System.Net.Http.Json;
using Credicarro.Authentication.Models;
using Credicarro.Errors;
using Credicarro.Serialization;

namespace Credicarro.Authentication;

internal sealed class TokenApiClient(HttpClient httpClient) : ITokenApiClient
{
    public async Task<GenerateAccessTokenResponse> GenerateAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var request = new GenerateAccessTokenRequest(clientId, clientSecret);

        using var response = await httpClient
            .PostAsJsonAsync("v1/authentication/accesstoken", request, CredicarroJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw await CredicarroExceptionFactory.FromTokenResponseAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.Content is null)
            throw new CredicarroAuthenticationException("A API do Credicarro retornou uma resposta vazia ao gerar o token de acesso.");

        GenerateAccessTokenResponse? result;

        try
        {
            result = await response.Content
                .ReadFromJsonAsync<GenerateAccessTokenResponse>(CredicarroJsonOptions.Default, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new CredicarroAuthenticationException("A API do Credicarro retornou uma resposta inválida ao gerar o token de acesso.", ex);
        }

        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new CredicarroAuthenticationException("A API do Credicarro retornou uma resposta vazia ao gerar o token de acesso.");

        return result;
    }
}
