using System.Net;
using Credicarro.Configuration;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Resilience;

public class ResilienceDelegatingHandlerTests
{
    private const string ProposalId = "bea8d060-f93d-48eb-b6c4-45f64483cdd9";
    private const string WebhookId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    private static CredicarroOptions Options(Action<CredicarroResilienceOptions>? configure = null)
    {
        var options = TestApiClientFactory.FastOptions();
        options.Resilience.CircuitBreakerMinimumThroughput = 10;
        options.Resilience.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
        options.Resilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500);
        configure?.Invoke(options.Resilience);
        return options;
    }

    private static FakeHttpMessageHandler FailThenSucceed(int failures, int failureStatus) =>
        new((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex <= failures ? failureStatus : 200)));

    private static FakeHttpMessageHandler AlwaysFail(int status) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Status(status)));

    // ---- Requisições seguras (GET)

    [Fact]
    public async Task SendAsync_GetCom503_TentaNovamenteAteSucesso()
    {
        var inner = FailThenSucceed(2, 503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task SendAsync_GetComStatusTransitorio_TentaNovamente(int status)
    {
        var inner = FailThenSucceed(1, status);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task SendAsync_ComOutros4xx_NuncaTentaNovamente(int status)
    {
        var inner = AlwaysFail(status);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        HttpStatusCode? result = null;
        try
        {
            result = (await client.GetAsync("v2/proposal/" + ProposalId)).StatusCode;
        }
        catch (CredicarroAuthenticationException)
        {
            // 401 é tratado pela renovação de token (2 chamadas), não pelo retry.
        }

        Assert.Equal(status == 401 ? 2 : 1, inner.CallCount);
        Assert.True(result is null || (int)result == status);
    }

    [Fact]
    public async Task SendAsync_ComFalhasPersistentes_EsgotaAsTentativasEDevolveOUltimoStatus()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal((HttpStatusCode)503, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_ComMaxRetryAttemptsUm_NaoTentaNovamente()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options(r => r.MaxRetryAttempts = 1));

        await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(1, inner.CallCount);
    }

    // ---- HTTP 500

    [Fact]
    public async Task SendAsync_GetCom500_TentaNovamentePorPadrao()
    {
        var inner = FailThenSucceed(1, 500);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_Com500EComRetryOn500Desabilitado_NaoTentaNovamente()
    {
        var inner = AlwaysFail(500);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options(r => r.RetryOn500 = false));

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_PostSemChaveCom500_NaoTentaNovamente_MesmoComRetryOn500()
    {
        var inner = AlwaysFail(500);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync("v1/webhook", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    // ---- POST com Idempotency-Key (automática)

    [Fact]
    public async Task SendAsync_PostDeCriacaoCom503_TentaNovamenteComAMesmaIdempotencyKey()
    {
        var inner = FailThenSucceed(2, 503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync("v2/proposal", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.CallCount);

        var keys = inner.Requests.Select(request => request.Header("Idempotency-Key")).ToList();
        Assert.All(keys, key => Assert.NotNull(key));
        Assert.Single(keys.Distinct());
    }

    [Fact]
    public async Task SendAsync_PostDeCriacaoCom500_TentaNovamenteComAMesmaChave()
    {
        var inner = FailThenSucceed(1, 500);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync("v1/customer", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
        Assert.Equal(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_PostDeCriacaoComDisableAutomaticIdempotencyKey_NaoTentaNovamente()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options(r => r.DisableAutomaticIdempotencyKey = true));

        await client.PostAsync("v2/proposal", new StringContent("{}"));

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_UploadDeDocumento_ReusaAMesmaIdempotencyKeyEmTodasAsTentativas()
    {
        var inner = FailThenSucceed(2, 502);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync("v2/proposal/" + ProposalId + "/document", new StringContent("{\"conteudoBase64\":\"AAAA\"}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
        Assert.Single(inner.Requests.Select(request => request.Header("Idempotency-Key")).Distinct());
        Assert.All(inner.Requests, request => Assert.Equal("{\"conteudoBase64\":\"AAAA\"}", request.Body));
    }

    // ---- POST customer/unified

    [Fact]
    public async Task SendAsync_UnifiedSemChave_NaoERepetidoENaoRecebeChaveAutomatica()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync("v1/customer/unified", new StringContent("{}"));

        Assert.Equal((HttpStatusCode)503, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_UnifiedSemChaveComFalhaDeRede_LancaTimeoutSemRepetir()
    {
        var inner = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("rede caiu"));
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.PostAsync("v1/customer/unified", new StringContent("{}")));

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_UnifiedComChaveExplicita_ERepetidoComAMesmaChave()
    {
        var inner = FailThenSucceed(1, 503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/customer/unified") { Content = new StringContent("{}") };
        request.Headers.Add("Idempotency-Key", "criar-cliente-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
        Assert.All(inner.Requests, r => Assert.Equal("criar-cliente-1", r.Header("Idempotency-Key")));
    }

    // ---- POST webhook (cadastro)

    [Fact]
    public async Task SendAsync_PostDeWebhook_NaoERepetido()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        await client.PostAsync("v1/webhook", new StringContent("{}"));

        Assert.Equal(1, inner.CallCount);
        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    // ---- Substituição e webhooks por id

    [Theory]
    [InlineData("vehicle-detail")]
    [InlineData("deposit-account")]
    public async Task SendAsync_PostDeVeiculoEContaDeRecebimento_ERepetidoPorTerSemanticaDeSubstituicao(string action)
    {
        var inner = FailThenSucceed(1, 503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.PostAsync($"v2/proposal/{ProposalId}/{action}", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_PutEDeleteDeWebhookPorId_SaoRepetidos()
    {
        var putInner = FailThenSucceed(1, 503);
        var putClient = TestApiClientFactory.CreateApiHttpClient(putInner, Options());
        await putClient.PutAsync("v1/webhook/" + WebhookId, new StringContent("{}"));

        var deleteInner = FailThenSucceed(1, 503);
        var deleteClient = TestApiClientFactory.CreateApiHttpClient(deleteInner, Options());
        await deleteClient.DeleteAsync("v1/webhook/" + WebhookId);

        Assert.Equal(2, putInner.CallCount);
        Assert.Equal(2, deleteInner.CallCount);
    }

    [Fact]
    public async Task SendAsync_PutEmOutraRota_NaoERepetido()
    {
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        await client.PutAsync("v2/proposal/" + ProposalId, new StringContent("{}"));

        Assert.Equal(1, inner.CallCount);
    }

    // ---- Retry-After, rede, timeout, circuit breaker, cancelamento

    [Fact]
    public async Task SendAsync_Com429ComRetryAfter_RespeitaODelayInformado()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) =>
        {
            if (callIndex == 1)
            {
                var limited = new HttpResponseMessage((HttpStatusCode)429);
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(300));
                return Task.FromResult(limited);
            }

            return Task.FromResult(TestApiClientFactory.Status(200));
        });
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync("v2/proposal/" + ProposalId);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
        Assert.True(stopwatch.ElapsedMilliseconds >= 250, $"Esperava respeitar o Retry-After de 300 ms, mas levou {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task SendAsync_GetComFalhaDeRede_TentaNovamente()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) =>
            callIndex == 1
                ? throw new HttpRequestException("rede caiu")
                : Task.FromResult(TestApiClientFactory.Status(200)));
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        var response = await client.GetAsync("v2/proposal/" + ProposalId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_PostDeCriacaoComFalhaDeRede_TentaNovamenteComAMesmaChave()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) =>
            callIndex == 1
                ? throw new HttpRequestException("rede caiu")
                : Task.FromResult(TestApiClientFactory.Status(200)));
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        await client.PostAsync("v2/proposal/simulation/financing", new StringContent("{}"));

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task SendAsync_ComFalhasConsecutivasAcimaDoLimite_AbreOCircuitBreaker()
    {
        var options = Options(r =>
        {
            r.MaxRetryAttempts = 1;
            r.CircuitBreakerMinimumThroughput = 2;
            r.CircuitBreakerFailureRatio = 0.5;
            r.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);
        });
        var inner = AlwaysFail(503);
        var client = TestApiClientFactory.CreateApiHttpClient(inner, options);

        await client.GetAsync("v2/proposal/" + ProposalId);
        await client.GetAsync("v2/proposal/" + ProposalId);

        var callsBeforeOpen = inner.CallCount;

        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.GetAsync("v2/proposal/" + ProposalId));

        Assert.Equal(callsBeforeOpen, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_ComTimeoutDeTentativaExcedido_LancaCredicarroTimeoutException()
    {
        var options = Options(r =>
        {
            r.AttemptTimeout = TimeSpan.FromMilliseconds(50);
            r.MaxRetryAttempts = 1;
        });
        var inner = new FakeHttpMessageHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return TestApiClientFactory.Status(200);
        });
        var client = TestApiClientFactory.CreateApiHttpClient(inner, options);

        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.GetAsync("v2/proposal/" + ProposalId));
    }

    [Fact]
    public async Task SendAsync_UploadDeDocumento_UsaOTimeoutDeTentativaMaiorQueAsDemaisRotas()
    {
        var options = Options(r =>
        {
            r.AttemptTimeout = TimeSpan.FromMilliseconds(50);
            r.DocumentUploadAttemptTimeout = TimeSpan.FromSeconds(3);
            r.MaxRetryAttempts = 1;
        });

        FakeHttpMessageHandler SlowThenOk() => new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            return TestApiClientFactory.Status(200);
        });

        var uploadClient = TestApiClientFactory.CreateApiHttpClient(SlowThenOk(), options);
        var upload = await uploadClient.PostAsync("v2/proposal/" + ProposalId + "/document", new StringContent("{}"));

        var getClient = TestApiClientFactory.CreateApiHttpClient(SlowThenOk(), options);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => getClient.GetAsync("v2/proposal/" + ProposalId));
    }

    [Fact]
    public async Task SendAsync_ComCancellationTokenCancelado_PropagaOCancelamento()
    {
        using var cts = new CancellationTokenSource();
        var inner = new FakeHttpMessageHandler(async (_, _) =>
        {
            cts.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
            return TestApiClientFactory.Status(200);
        });
        var client = TestApiClientFactory.CreateApiHttpClient(inner, Options());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("v2/proposal/" + ProposalId, cts.Token));
    }
}
