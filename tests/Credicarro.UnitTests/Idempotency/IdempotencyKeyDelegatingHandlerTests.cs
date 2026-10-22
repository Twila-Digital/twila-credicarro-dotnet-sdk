using Credicarro.Configuration;
using Credicarro.Idempotency;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Idempotency;

public class IdempotencyKeyDelegatingHandlerTests
{
    private const string ProposalId = "bea8d060-f93d-48eb-b6c4-45f64483cdd9";

    private static HttpClient BuildClient(FakeHttpMessageHandler inner, CredicarroOptions? options = null)
    {
        var handler = new IdempotencyKeyDelegatingHandler(options ?? new CredicarroOptions { ClientId = "id", ClientSecret = "secret" })
        {
            InnerHandler = inner
        };
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.credicarro.com.br/integration/") };
    }

    private static FakeHttpMessageHandler Ok() =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Status(200)));

    [Theory]
    [InlineData("v1/customer")]
    [InlineData("v1/customer/address")]
    [InlineData("v1/customer/professional")]
    [InlineData("v1/proposal/simulation/financing")]
    [InlineData("v1/proposal/simulation/refinancing")]
    [InlineData("v2/proposal/simulation/financing")]
    [InlineData("v2/proposal/simulation/refinancing")]
    [InlineData("v1/proposal")]
    [InlineData("v2/proposal")]
    [InlineData("v2/proposal/" + ProposalId + "/document")]
    public async Task SendAsync_EmRotaDeCriacao_AnexaIdempotencyKey(string path)
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.PostAsync(path, new StringContent("{}"));

        var key = inner.Requests[0].Header("Idempotency-Key");
        Assert.NotNull(key);
        Assert.True(Guid.TryParse(key, out _));
    }

    [Theory]
    [InlineData("v1/customer/unified")]
    [InlineData("v1/webhook")]
    [InlineData("v1/authentication/accesstoken")]
    [InlineData("v2/proposal/" + ProposalId + "/vehicle-detail")]
    [InlineData("v2/proposal/" + ProposalId + "/deposit-account")]
    public async Task SendAsync_EmRotaSemChaveAutomatica_NaoAnexaIdempotencyKey(string path)
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.PostAsync(path, new StringContent("{}"));

        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_EmUnified_NuncaAnexaChaveAutomatica_MesmoComAConfiguracaoPadrao()
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.PostAsync("v1/customer/unified", new StringContent("{}"));
        await client.PostAsync("v1/customer/unified", new StringContent("{}"));

        Assert.All(inner.Requests, request => Assert.False(request.HasHeader("Idempotency-Key")));
    }

    [Theory]
    [InlineData("v1/proposal/simulation/range/financing")]
    [InlineData("v2/proposal/" + ProposalId)]
    [InlineData("v1/webhook")]
    public async Task SendAsync_EmGet_NaoAnexaIdempotencyKey(string path)
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.GetAsync(path);

        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_EmPutEDeleteDeWebhook_NaoAnexaIdempotencyKey()
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.PutAsync("v1/webhook/3fa85f64-5717-4562-b3fc-2c963f66afa6", new StringContent("{}"));
        await client.DeleteAsync("v1/webhook/3fa85f64-5717-4562-b3fc-2c963f66afa6");

        Assert.All(inner.Requests, request => Assert.False(request.HasHeader("Idempotency-Key")));
    }

    [Fact]
    public async Task SendAsync_ChamadasDiferentesParaAMesmaRota_GeramChavesDiferentes()
    {
        var inner = Ok();
        var client = BuildClient(inner);

        await client.PostAsync("v2/proposal", new StringContent("{}"));
        await client.PostAsync("v2/proposal", new StringContent("{}"));

        Assert.NotEqual(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_ComChaveExplicitaNaRequisicao_PreservaAChave()
    {
        var inner = Ok();
        var client = BuildClient(inner);
        var request = new HttpRequestMessage(HttpMethod.Post, "v2/proposal") { Content = new StringContent("{}") };
        request.Headers.Add("Idempotency-Key", "minha-chave");

        await client.SendAsync(request);

        Assert.Equal("minha-chave", inner.Requests[0].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_ComDisableAutomaticIdempotencyKey_NaoAnexaNadaMesmoEmRotaDeCriacao()
    {
        var options = new CredicarroOptions { ClientId = "id", ClientSecret = "secret" };
        options.Resilience.DisableAutomaticIdempotencyKey = true;

        var inner = Ok();
        var client = BuildClient(inner, options);

        await client.PostAsync("v2/proposal", new StringContent("{}"));

        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_ComDisableAutomaticIdempotencyKey_PreservaChaveExplicita()
    {
        var options = new CredicarroOptions { ClientId = "id", ClientSecret = "secret" };
        options.Resilience.DisableAutomaticIdempotencyKey = true;

        var inner = Ok();
        var client = BuildClient(inner, options);
        var request = new HttpRequestMessage(HttpMethod.Post, "v2/proposal/" + ProposalId + "/document") { Content = new StringContent("{}") };
        request.Headers.Add("Idempotency-Key", "doc-1");

        await client.SendAsync(request);

        Assert.Equal("doc-1", inner.Requests[0].Header("Idempotency-Key"));
    }
}
