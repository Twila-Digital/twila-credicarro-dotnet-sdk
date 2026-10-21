using System.Net;
using Credicarro.Configuration;
using Credicarro.Customers.Models;
using Credicarro.DependencyInjection;
using Credicarro.Errors;
using Credicarro.Proposals.Models;
using Credicarro.UnitTests.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Credicarro.UnitTests.DependencyInjection;

/// <summary>
/// Testes ponta a ponta da pipeline real registrada por <c>AddCredicarro</c>, trocando só o handler primário.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    private static readonly Guid CustomerId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
    private static readonly Guid ProposalId = Guid.Parse("bea8d060-f93d-48eb-b6c4-45f64483cdd9");

    private const string CustomerJson = """{ "id_cliente": "123e4567-e89b-12d3-a456-426614174000" }""";

    private static ServiceProvider Build(HttpMessageHandler primary, Action<CredicarroOptions>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddCredicarro(options =>
        {
            options.ClientId = "meu-id";
            options.ClientSecret = "meu-segredo";
            options.Environment = CredicarroEnvironment.Staging;
            options.Resilience.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
            configure?.Invoke(options);
        });

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = primary));

        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage TokenResponse(int tokenNumber) =>
        TestApiClientFactory.Json($$"""
            {
                "token_de_acesso": "token-{{tokenNumber}}",
                "expira_em_segundos": 3600,
                "expira_em": "{{DateTimeOffset.UtcNow.AddMinutes(59):o}}",
                "tipo_de_token": "Bearer",
                "escopo": "credicarro.integration.access/full"
            }
            """);

    private static bool IsTokenRequest(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/v1/authentication/accesstoken", StringComparison.Ordinal);

    /// <summary>
    /// Handler que atende o token e delega as demais rotas. <c>tokens</c> conta quantos tokens foram emitidos.
    /// </summary>
    private sealed class Api
    {
        private int _tokens;

        public int TokenCalls => _tokens;

        public FakeHttpMessageHandler Handler { get; }

        public Api(Func<HttpRequestMessage, int, HttpResponseMessage> api)
        {
            var apiCalls = 0;
            Handler = new FakeHttpMessageHandler((request, _) =>
            {
                if (IsTokenRequest(request))
                    return Task.FromResult(TokenResponse(Interlocked.Increment(ref _tokens)));

                return Task.FromResult(api(request, Interlocked.Increment(ref apiCalls)));
            });
        }

        public IEnumerable<RecordedRequest> ApiRequests => Handler.Requests.Where(request => !request.Path.EndsWith("/accesstoken", StringComparison.Ordinal));
    }

    private static CreateCustomerRequest NewCustomer() =>
        new("12345678909", "Maria", "Souza", "maria@exemplo.com.br", "+55 (11) 99876-5432", new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)));

    [Fact]
    public async Task ObtemOTokenUmaVezEOReaproveitaNasChamadasSeguintes()
    {
        var api = new Api((_, _) => TestApiClientFactory.Json(CustomerJson));
        using var provider = Build(api.Handler);
        var client = provider.GetRequiredService<ICredicarroClient>();

        await client.Customers.CreateAsync(NewCustomer());
        await client.Customers.CreateAsync(NewCustomer());

        Assert.Equal(1, api.TokenCalls);
        Assert.All(api.ApiRequests, request => Assert.Equal("Bearer token-1", request.Header("Authorization")));

        var tokenRequest = api.Handler.Requests.First(request => request.Path.EndsWith("/accesstoken", StringComparison.Ordinal));
        Assert.Equal("/integration/v1/authentication/accesstoken", tokenRequest.Path);
        Assert.Equal("api.staging.credicarro.com.br", tokenRequest.Uri.Host);
        Assert.Contains("\"clientId\":\"meu-id\"", tokenRequest.Body, StringComparison.Ordinal);
        Assert.False(tokenRequest.HasHeader("Authorization"));
    }

    [Fact]
    public async Task ComEnvironmentProduction_UsaOHostDeProducao()
    {
        var api = new Api((_, _) => TestApiClientFactory.Json(CustomerJson));
        using var provider = Build(api.Handler, options => options.Environment = CredicarroEnvironment.Production);

        await provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(NewCustomer());

        Assert.All(api.Handler.Requests, request => Assert.Equal("api.credicarro.com.br", request.Uri.Host));
    }

    [Fact]
    public async Task Com401NaRotaAutenticada_RenovaOTokenERepeteUmaVez()
    {
        var api = new Api((_, callIndex) => callIndex == 1 ? TestApiClientFactory.Status(401) : TestApiClientFactory.Json(CustomerJson));
        using var provider = Build(api.Handler);

        var id = await provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(NewCustomer());

        Assert.Equal(CustomerId, id);
        Assert.Equal(2, api.TokenCalls);
        var calls = api.ApiRequests.ToList();
        Assert.Equal(2, calls.Count);
        Assert.Equal("Bearer token-1", calls[0].Header("Authorization"));
        Assert.Equal("Bearer token-2", calls[1].Header("Authorization"));
    }

    [Fact]
    public async Task Com401Persistente_LancaAuthenticationException()
    {
        var api = new Api((_, _) => TestApiClientFactory.Status(401));
        using var provider = Build(api.Handler);

        await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(NewCustomer()));

        Assert.Equal(2, api.ApiRequests.Count());
    }

    [Fact]
    public async Task ChamadasConcorrentes_ObtemApenasUmToken()
    {
        var api = new Api((_, _) => TestApiClientFactory.Json(CustomerJson));
        using var provider = Build(api.Handler);
        var client = provider.GetRequiredService<ICredicarroClient>();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.Customers.CreateAsync(NewCustomer())));

        Assert.Equal(1, api.TokenCalls);
        Assert.Equal(20, api.ApiRequests.Count());
    }

    [Fact]
    public async Task CredenciaisInvalidas_LancamAuthenticationExceptionComOCodigoEstavel()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(
            """{ "tipo": "Integration.Authentication.InvalidCredentials", "titulo": "Credenciais inválidas", "status": 401 }""",
            HttpStatusCode.Unauthorized)));
        using var provider = Build(handler);

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(NewCustomer()));

        Assert.Equal("Integration.Authentication.InvalidCredentials", exception.ErrorCode);
        Assert.DoesNotContain("meu-segredo", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ATentativaDeGerarOTokenTambemTemRetry()
    {
        var tokenCalls = 0;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            if (IsTokenRequest(request))
                return Task.FromResult(Interlocked.Increment(ref tokenCalls) == 1 ? TestApiClientFactory.Status(503) : TokenResponse(1));

            return Task.FromResult(TestApiClientFactory.Json(CustomerJson));
        });
        using var provider = Build(handler);

        var id = await provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(NewCustomer());

        Assert.Equal(CustomerId, id);
        Assert.Equal(2, tokenCalls);
    }

    [Fact]
    public async Task Unified_SemChave_NaoERepetidoENaoRecebeChaveAutomatica()
    {
        var api = new Api((_, _) => TestApiClientFactory.Status(503));
        using var provider = Build(api.Handler);
        var request = new UpsertCustomerRequest(
            "12345678909", "Maria", "Souza", "maria@exemplo.com.br", "+55 (11) 99876-5432",
            new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
            new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000"),
            new ProfessionalData(OccupationType.Employee, 6500m));

        await Assert.ThrowsAsync<CredicarroApiException>(() => provider.GetRequiredService<ICredicarroClient>().Customers.UpsertAsync(request));

        var calls = api.ApiRequests.ToList();
        Assert.Single(calls);
        Assert.False(calls[0].HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task Unified_ComChaveExplicita_ERepetidoComAMesmaChave()
    {
        var api = new Api((_, callIndex) => callIndex == 1 ? TestApiClientFactory.Status(503) : TestApiClientFactory.Json(CustomerJson));
        using var provider = Build(api.Handler);
        var request = new UpsertCustomerRequest(
            "12345678909", "Maria", "Souza", "maria@exemplo.com.br", "+55 (11) 99876-5432",
            new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
            new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000"),
            new ProfessionalData(OccupationType.Employee, 6500m));

        var id = await provider.GetRequiredService<ICredicarroClient>().Customers.UpsertAsync(request, "criar-maria");

        Assert.Equal(CustomerId, id);
        var calls = api.ApiRequests.ToList();
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal("criar-maria", call.Header("Idempotency-Key")));
    }

    [Fact]
    public async Task UploadDeDocumento_ComFalhasTransitorias_ReusaAMesmaChaveEOMesmoCorpo()
    {
        var api = new Api((_, callIndex) => TestApiClientFactory.Status(callIndex <= 2 ? 502 : 204));
        using var provider = Build(api.Handler);
        var file = DocumentFile.FromBytes(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "cnh.pdf");

        await provider.GetRequiredService<ICredicarroClient>().Proposals.UploadDocumentAsync(
            ProposalId,
            new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, file));

        var calls = api.ApiRequests.ToList();
        Assert.Equal(3, calls.Count);
        Assert.Single(calls.Select(call => call.Header("Idempotency-Key")).Distinct());
        Assert.Single(calls.Select(call => call.Body).Distinct());
    }

    [Fact]
    public async Task CriarWebhook_NaoERepetido()
    {
        var api = new Api((_, _) => TestApiClientFactory.Status(503));
        using var provider = Build(api.Handler);

        await Assert.ThrowsAsync<CredicarroApiException>(
            () => provider.GetRequiredService<ICredicarroClient>().Webhooks.CreateAsync(
                new Credicarro.Webhooks.Models.CreateWebhookRequest(
                    Credicarro.Webhooks.Models.WebhookType.Proposal,
                    "https://parceiro.com.br/w",
                    Credicarro.Webhooks.Models.WebhookAuthenticationType.None)));

        Assert.Single(api.ApiRequests);
    }

    [Fact]
    public void ExpoTodosOsClientesDeRecurso()
    {
        using var provider = Build(new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(200))));

        var client = provider.GetRequiredService<ICredicarroClient>();

        Assert.NotNull(client.Customers);
        Assert.NotNull(client.Simulations);
        Assert.NotNull(client.Proposals);
        Assert.NotNull(client.Webhooks);
        Assert.NotNull(client.V1);
        Assert.NotNull(client.V1.Simulations);
        Assert.NotNull(client.V1.Proposals);
        Assert.Same(client, provider.GetRequiredService<ICredicarroClient>());
    }

    [Fact]
    public void ComClientIdVazio_LancaConfigurationException_SemExporOSegredo()
    {
        using var provider = Build(
            new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(200))),
            options =>
            {
                options.ClientId = "";
                options.ClientSecret = "segredo-que-nao-pode-vazar";
            });

        var exception = Assert.Throws<CredicarroConfigurationException>(() => provider.GetRequiredService<ICredicarroClient>());

        Assert.Contains("ClientId", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-que-nao-pode-vazar", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComBaseUrlHttpNaoLoopback_LancaConfigurationException()
    {
        using var provider = Build(
            new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(200))),
            options => options.BaseUrl = new Uri("http://api.exemplo.com/integration"));

        var exception = Assert.Throws<CredicarroConfigurationException>(() => provider.GetRequiredService<ICredicarroClient>());

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }
}
