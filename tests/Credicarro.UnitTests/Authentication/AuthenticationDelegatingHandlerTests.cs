using System.Net;
using Credicarro.Authentication;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Authentication;

public class AuthenticationDelegatingHandlerTests
{
    private sealed class FakeTokenProvider : IAccessTokenProvider
    {
        private int _tokenVersion;

        public List<string> InvalidatedTokens { get; } = [];

        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult($"token-v{_tokenVersion}");

        public void Invalidate(string rejectedToken)
        {
            InvalidatedTokens.Add(rejectedToken);
            _tokenVersion++;
        }
    }

    private static HttpClient BuildClient(FakeHttpMessageHandler inner, FakeTokenProvider tokenProvider)
    {
        var authHandler = new AuthenticationDelegatingHandler(tokenProvider) { InnerHandler = inner };
        return new HttpClient(authHandler) { BaseAddress = new Uri("https://api.credicarro.com.br/integration/") };
    }

    [Fact]
    public async Task SendAsync_AnexaBearerTokenNaRequisicao()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(200)));

        var client = BuildClient(inner, new FakeTokenProvider());
        await client.GetAsync("v2/proposal/123");

        Assert.Equal("Bearer token-v0", inner.Requests[0].Header("Authorization"));
    }

    [Fact]
    public async Task SendAsync_Com401_RenovaOTokenERepeteUmaUnicaVezComONovoToken()
    {
        var tokenProvider = new FakeTokenProvider();
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex == 1 ? 401 : 200)));

        var client = BuildClient(inner, tokenProvider);
        var response = await client.GetAsync("v2/proposal/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["token-v0"], tokenProvider.InvalidatedTokens);
        Assert.Equal(2, inner.CallCount);
        Assert.Equal("Bearer token-v0", inner.Requests[0].Header("Authorization"));
        Assert.Equal("Bearer token-v1", inner.Requests[1].Header("Authorization"));
    }

    [Fact]
    public async Task SendAsync_Com401Persistente_LancaCredicarroAuthenticationException_SemLoopInfinito()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(401)));

        var client = BuildClient(inner, new FakeTokenProvider());

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(() => client.GetAsync("v2/proposal/123"));

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsync_Com401SemCorpo_UsaMensagemClaraEmPortugues()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(401)));

        var client = BuildClient(inner, new FakeTokenProvider());

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(() => client.GetAsync("v2/proposal/123"));

        Assert.Contains("Não autorizado", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_ComCorpoNaRequisicao_ReenviaOMesmoCorpoNaSegundaTentativa()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex == 1 ? 401 : 200)));

        var client = BuildClient(inner, new FakeTokenProvider());
        await client.PostAsync("v2/proposal", new StringContent("{\"cpf\":\"12345678909\"}"));

        Assert.Equal(2, inner.Requests.Count);
        Assert.All(inner.Requests, request => Assert.Equal("{\"cpf\":\"12345678909\"}", request.Body));
    }
}
