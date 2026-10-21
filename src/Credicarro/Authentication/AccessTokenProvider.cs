using Credicarro.Configuration;

namespace Credicarro.Authentication;

/// <summary>
/// Mantém o token em cache até <c>expira_em</c> (UTC, já com 60 segundos de margem). Sem <c>expira_em</c>, usa
/// <c>agora + max(expira_em_segundos / 2, expira_em_segundos - 60)</c>. A renovação é single-flight: chamadas concorrentes esperam a mesma renovação.
/// </summary>
internal sealed class AccessTokenProvider(ITokenApiClient tokenApiClient, CredicarroOptions options) : IAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AccessToken? _cached;

    /// <summary>
    /// Relógio usado para decidir a expiração. Substituível nos testes.
    /// </summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref _cached);
        if (current is not null && !current.IsExpired(Clock()))
            return current.Value;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = Volatile.Read(ref _cached);
            if (current is not null && !current.IsExpired(Clock()))
                return current.Value;

            var response = await tokenApiClient.GenerateAsync(options.ClientId, options.ClientSecret, cancellationToken).ConfigureAwait(false);
            var expiresAt = response.ExpiresAt ?? Clock().Add(FallbackValidity(response.ExpiresInSeconds));
            var fresh = new AccessToken(response.AccessToken, expiresAt);

            Volatile.Write(ref _cached, fresh);
            return fresh.Value;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sem <c>expira_em</c>: <c>max(expira_em_segundos / 2, expira_em_segundos - 60)</c>. Assim um token curto
    /// (60 s ou menos) ainda é reaproveitado por metade da validade em vez de ser renovado a cada chamada.
    /// </summary>
    private static TimeSpan FallbackValidity(int expiresInSeconds)
    {
        var seconds = Math.Max(expiresInSeconds, 0);
        return TimeSpan.FromSeconds(Math.Max(seconds / 2.0, seconds - SafetyMargin.TotalSeconds));
    }

    public void Invalidate(string rejectedToken)
    {
        var current = Volatile.Read(ref _cached);

        if (current is not null && string.Equals(current.Value, rejectedToken, StringComparison.Ordinal))
            Interlocked.CompareExchange(ref _cached, null, current);
    }

    public void Dispose() => _lock.Dispose();
}
