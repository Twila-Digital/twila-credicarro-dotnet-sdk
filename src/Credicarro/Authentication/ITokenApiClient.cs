using Credicarro.Authentication.Models;

namespace Credicarro.Authentication;

internal interface ITokenApiClient
{
    Task<GenerateAccessTokenResponse> GenerateAsync(string clientId, string clientSecret, CancellationToken cancellationToken);
}
