namespace Credicarro.Authentication;

/// <summary>
/// Token em cache. <see cref="ExpiresAt"/> (UTC) é o instante até o qual o token pode ser reaproveitado; a margem de
/// segurança de 60 segundos já vem aplicada.
/// </summary>
internal sealed record AccessToken(string Value, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Não inclui o valor do token.
    /// </summary>
    public override string ToString() => $"AccessToken {{ ExpiresAt = {ExpiresAt:o} }}";
}
