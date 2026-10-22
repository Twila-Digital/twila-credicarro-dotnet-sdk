namespace Credicarro.Authentication;

internal interface IAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Descarta o token em cache, mas só se ele ainda for o <paramref name="rejectedToken"/>. Se outra chamada já
    /// renovou o token, o novo é preservado.
    /// </summary>
    void Invalidate(string rejectedToken);
}
