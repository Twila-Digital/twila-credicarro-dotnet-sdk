using System.Text.Json.Serialization;

namespace Credicarro.Authentication.Models;

internal sealed record GenerateAccessTokenResponse(
    [property: JsonPropertyName("token_de_acesso")] string AccessToken,
    [property: JsonPropertyName("expira_em_segundos")] int ExpiresInSeconds,
    [property: JsonPropertyName("expira_em")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("tipo_de_token")] string? TokenType,
    [property: JsonPropertyName("escopo")] string? Scope)
{
    /// <summary>
    /// Não inclui o token.
    /// </summary>
    public override string ToString() =>
        $"GenerateAccessTokenResponse {{ ExpiresInSeconds = {ExpiresInSeconds}, ExpiresAt = {ExpiresAt:o}, TokenType = {TokenType}, Scope = {Scope} }}";
}
