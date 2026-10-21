using System.Text.Json.Serialization;

namespace Credicarro.Authentication.Models;

internal sealed record GenerateAccessTokenRequest(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientSecret")] string ClientSecret)
{
    /// <summary>
    /// Nunca inclui o <c>clientSecret</c>.
    /// </summary>
    public override string ToString() => $"GenerateAccessTokenRequest {{ ClientId = {ClientId}, ClientSecret = *** }}";
}
