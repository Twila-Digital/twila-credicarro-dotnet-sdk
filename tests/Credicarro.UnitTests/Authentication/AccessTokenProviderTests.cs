using Credicarro.Authentication;
using Credicarro.Authentication.Models;
using Credicarro.Configuration;
using Credicarro.Errors;

namespace Credicarro.UnitTests.Authentication;

public class AccessTokenProviderTests
{
    private sealed class CountingTokenApiClient(Func<int, GenerateAccessTokenResponse> respond) : ITokenApiClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<GenerateAccessTokenResponse> GenerateAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            var callIndex = Interlocked.Increment(ref _callCount);
            return Task.FromResult(respond(callIndex));
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static CredicarroOptions Options() => new() { ClientId = "id", ClientSecret = "secret" };

    private static GenerateAccessTokenResponse Response(string token, int seconds, DateTimeOffset? expiresAt = null) =>
        new(token, seconds, expiresAt, "Bearer", "credicarro.integration.access/full");

    [Fact]
    public async Task GetTokenAsync_PrimeiraChamada_GeraUmTokenNovo()
    {
        var tokenApiClient = new CountingTokenApiClient(_ => Response("token-1", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        var token = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", token);
        Assert.Equal(1, tokenApiClient.CallCount);
    }

    [Fact]
    public async Task GetTokenAsync_UsaOExpiraEmDaRespostaSemSubtrairNovamenteAMargem()
    {
        var now = Start;
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600, Start.AddSeconds(3540)));
        var provider = new AccessTokenProvider(tokenApiClient, Options()) { Clock = () => now };

        await provider.GetTokenAsync(CancellationToken.None);

        now = Start.AddSeconds(3539);
        var stillValid = await provider.GetTokenAsync(CancellationToken.None);

        now = Start.AddSeconds(3540);
        var renewed = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", stillValid);
        Assert.Equal("token-2", renewed);
        Assert.Equal(2, tokenApiClient.CallCount);
    }

    [Fact]
    public async Task GetTokenAsync_SemExpiraEm_UsaExpiraEmSegundosMenos60Segundos()
    {
        var now = Start;
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options()) { Clock = () => now };

        await provider.GetTokenAsync(CancellationToken.None);

        now = Start.AddSeconds(3539);
        var stillValid = await provider.GetTokenAsync(CancellationToken.None);

        now = Start.AddSeconds(3541);
        var renewed = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", stillValid);
        Assert.Equal("token-2", renewed);
    }

    [Fact]
    public async Task GetTokenAsync_ComTokenAindaValido_NaoGeraNovoToken()
    {
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(1, tokenApiClient.CallCount);
    }

    [Theory]
    [InlineData(30, 14, false)]
    [InlineData(30, 16, true)]
    [InlineData(60, 29, false)]
    [InlineData(60, 31, true)]
    [InlineData(120, 59, false)]
    [InlineData(120, 61, true)]
    [InlineData(3600, 3539, false)]
    [InlineData(3600, 3541, true)]
    public async Task GetTokenAsync_SemExpiraEm_UsaOMaiorEntreMetadeEValidadeMenos60(int seconds, int elapsed, bool renews)
    {
        var now = Start;
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", seconds));
        var provider = new AccessTokenProvider(tokenApiClient, Options()) { Clock = () => now };

        await provider.GetTokenAsync(CancellationToken.None);
        now = Start.AddSeconds(elapsed);
        await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal(renews ? 2 : 1, tokenApiClient.CallCount);
    }


    [Fact]
    public async Task Invalidate_ComOTokenRejeitadoAindaAtual_ForcaUmNovoToken()
    {
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        var first = await provider.GetTokenAsync(CancellationToken.None);
        provider.Invalidate(first);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("token-2", second);
        Assert.Equal(2, tokenApiClient.CallCount);
    }

    [Fact]
    public async Task Invalidate_ComTokenRejeitadoQueJaFoiSubstituido_PreservaOTokenNovo()
    {
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        var stale = await provider.GetTokenAsync(CancellationToken.None);
        provider.Invalidate(stale);
        var current = await provider.GetTokenAsync(CancellationToken.None);

        provider.Invalidate(stale);
        var afterStaleInvalidation = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal(current, afterStaleInvalidation);
        Assert.Equal(2, tokenApiClient.CallCount);
    }

    [Fact]
    public async Task GetTokenAsync_Com100ChamadasConcorrentes_GeraApenasUmToken()
    {
        var tokenApiClient = new CountingTokenApiClient(callIndex => Response($"token-{callIndex}", 3600));
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        var tasks = Enumerable.Range(0, 100).Select(_ => provider.GetTokenAsync(CancellationToken.None));
        var tokens = await Task.WhenAll(tasks);

        Assert.Equal(1, tokenApiClient.CallCount);
        Assert.All(tokens, t => Assert.Equal("token-1", t));
    }

    [Fact]
    public async Task GetTokenAsync_QuandoAGeracaoFalha_PropagaOErroENaoGuardaNada()
    {
        var calls = 0;
        var tokenApiClient = new FailingThenSucceedingClient(() => ++calls);
        var provider = new AccessTokenProvider(tokenApiClient, Options());

        await Assert.ThrowsAsync<CredicarroAuthenticationException>(() => provider.GetTokenAsync(CancellationToken.None));
        var token = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("token-ok", token);
        Assert.Equal(2, calls);
    }

    private sealed class FailingThenSucceedingClient(Func<int> next) : ITokenApiClient
    {
        public Task<GenerateAccessTokenResponse> GenerateAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            if (next() == 1)
                throw new CredicarroAuthenticationException("credenciais inválidas");

            return Task.FromResult(Response("token-ok", 3600));
        }
    }

    [Fact]
    public void AccessToken_ToString_NaoExpoeOValorDoToken()
    {
        var token = new AccessToken("super-token-secreto", Start);

        Assert.DoesNotContain("super-token-secreto", token.ToString(), StringComparison.Ordinal);
    }
}
