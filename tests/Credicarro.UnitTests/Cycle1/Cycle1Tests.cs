using System.Diagnostics;
using System.Net;
using System.Text;
using Credicarro.Configuration;
using Credicarro.Customers;
using Credicarro.Customers.Models;
using Credicarro.DependencyInjection;
using Credicarro.Errors;
using Credicarro.Http;
using Credicarro.Proposals;
using Credicarro.Proposals.Models;
using Credicarro.Simulations;
using Credicarro.Simulations.Models;
using Credicarro.UnitTests.TestUtilities;
using Credicarro.V1.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Credicarro.UnitTests.Cycle1;

/// <summary>
/// Convenções do ciclo 1: orçamento de upload, Retry-After, redirects, falha de rede, circuit breaker, buffer único,
/// idempotência opcional e máscaras de PII.
/// </summary>
public class Cycle1Tests
{
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

    private static bool IsToken(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/accesstoken", StringComparison.Ordinal);

    private static HttpResponseMessage Token() =>
        TestApiClientFactory.Json("""{ "token_de_acesso": "t", "expira_em_segundos": 3600, "tipo_de_token": "Bearer" }""");

    private static CredicarroOptions Options(Action<CredicarroResilienceOptions>? configure = null)
    {
        var options = TestApiClientFactory.FastOptions();
        configure?.Invoke(options.Resilience);
        return options;
    }

    private static DocumentFile Pdf() => DocumentFile.FromBytes(Encoding.UTF8.GetBytes("%PDF-1.7 conteudo"), "cnh.pdf");

    [Fact]
    public void ClientesHttp_TemTimeoutInfinitoEPrimarioSemRedirect()
    {
        using var provider = new ServiceCollection()
            .AddCredicarroForTests()
            .BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        foreach (var name in new[] { "credicarro.api", "credicarro.auth" })
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, factory.CreateClient(name).Timeout);

            var handler = handlerFactory.CreateHandler(name);
            while (handler is DelegatingHandler delegating && delegating.InnerHandler is not null)
                handler = delegating.InnerHandler;

            var primary = Assert.IsType<HttpClientHandler>(handler);
            Assert.False(primary.AllowAutoRedirect);
        }
    }

    [Fact]
    public async Task UploadContraServidorMudo_RespeitaOOrcamentoEMapeiaParaTimeout()
    {
        var calls = 0;
        var handler = new FakeHttpMessageHandler(async (request, _, cancellationToken) =>
        {
            if (IsToken(request))
                return Token();

            Interlocked.Increment(ref calls);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return TestApiClientFactory.Status(204);
        });

        using var provider = Build(handler, options =>
        {
            options.Resilience.AttemptTimeout = TimeSpan.FromMilliseconds(100);
            options.Resilience.DocumentUploadAttemptTimeout = TimeSpan.FromMilliseconds(250);
            options.Resilience.MaxRetryAttempts = 3;
        });
        var client = provider.GetRequiredService<ICredicarroClient>();
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.Proposals.UploadDocumentAsync(
            ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf())));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Levou {stopwatch.Elapsed}, esperava respeitar o orçamento de ~750 ms.");
        Assert.True(calls >= 2, "O upload deveria ter sido repetido dentro do orçamento (mesma chave).");
    }

    [Fact]
    public async Task OutrasChamadasUsamOTotalTimeout_NaoOOrcamentoDoUpload()
    {
        var handler = new FakeHttpMessageHandler(async (request, _, cancellationToken) =>
        {
            if (IsToken(request))
                return Token();

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return TestApiClientFactory.Status(200);
        });

        using var provider = Build(handler, options =>
        {
            options.Resilience.AttemptTimeout = TimeSpan.FromMilliseconds(100);
            options.Resilience.TotalTimeout = TimeSpan.FromMilliseconds(400);
            options.Resilience.DocumentUploadAttemptTimeout = TimeSpan.FromSeconds(30);
        });
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<CredicarroTimeoutException>(
            () => provider.GetRequiredService<ICredicarroClient>().Proposals.GetAsync(ProposalId));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static FakeHttpMessageHandler RateLimited(Action<HttpResponseMessage> configure, int succeedAfter = int.MaxValue) =>
        new((_, callIndex) =>
        {
            if (callIndex >= succeedAfter)
                return Task.FromResult(TestApiClientFactory.Json("""{ "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9" }"""));

            var response = TestApiClientFactory.Json("""{ "tipo": "RateLimit.Exceeded", "titulo": "Muitas requisições" }""", (HttpStatusCode)429);
            configure(response);
            return Task.FromResult(response);
        });

    [Fact]
    public async Task RetryAfterMaiorQueOOrcamentoRestante_LancaRateLimitNaHoraSemEsperar()
    {
        var inner = RateLimited(response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1)));
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, Options()));
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<CredicarroRateLimitException>(() => client.GetAsync(Guid.NewGuid()));

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(TimeSpan.FromHours(1), exception.RetryAfter);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RetryAfterAbsurdo_ELimitadoA24HorasELancaRateLimitNaHora()
    {
        var inner = RateLimited(response => response.Headers.TryAddWithoutValidation("Retry-After", "2000000000"));
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, Options()));

        var exception = await Assert.ThrowsAsync<CredicarroRateLimitException>(() => client.GetAsync(Guid.NewGuid()));

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(TimeSpan.FromHours(24), exception.RetryAfter);
    }

    [Fact]
    public async Task RetryAfterComDataHttpFutura_LancaRateLimitNaHora()
    {
        var inner = RateLimited(response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddHours(2)));
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, Options()));

        var exception = await Assert.ThrowsAsync<CredicarroRateLimitException>(() => client.GetAsync(Guid.NewGuid()));

        Assert.Equal(1, inner.CallCount);
        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter!.Value, TimeSpan.FromMinutes(119), TimeSpan.FromMinutes(121));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public async Task RetryAfterInvalidoOuNaoPositivo_EIgnoradoEUsaOBackoffNormal(string value)
    {
        var inner = RateLimited(response => response.Headers.TryAddWithoutValidation("Retry-After", value), succeedAfter: 2);
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, Options()));

        var simulation = await client.GetAsync(Guid.NewGuid());

        Assert.NotNull(simulation);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task RetryAfterCurto_EEsperadoEARepeticaoOcorre()
    {
        var inner = RateLimited(response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(200)), succeedAfter: 2);
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, Options()));
        var stopwatch = Stopwatch.StartNew();

        await client.GetAsync(Guid.NewGuid());

        Assert.Equal(2, inner.CallCount);
        Assert.True(stopwatch.ElapsedMilliseconds >= 150);
    }

    [Fact]
    public void RetryAfterHeader_LimitaEIgnora()
    {
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        HttpResponseMessage With(System.Net.Http.Headers.RetryConditionHeaderValue value)
        {
            var response = new HttpResponseMessage();
            response.Headers.RetryAfter = value;
            return response;
        }

        Assert.Null(RetryAfterHeader.From(new HttpResponseMessage(), now));
        Assert.Null(RetryAfterHeader.From(With(new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero)), now));
        Assert.Null(RetryAfterHeader.From(With(new System.Net.Http.Headers.RetryConditionHeaderValue(now.AddMinutes(-5))), now));
        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterHeader.From(With(new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30))), now));
        Assert.Equal(TimeSpan.FromHours(24), RetryAfterHeader.From(With(new System.Net.Http.Headers.RetryConditionHeaderValue(now.AddYears(50))), now));
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    public async Task Redirect_ViraApiException_SemSerRepetido(int status)
    {
        var inner = new FakeHttpMessageHandler((_, _) =>
        {
            var response = TestApiClientFactory.Status(status);
            response.Headers.Location = new Uri("https://outro-host.exemplo.com/");
            return Task.FromResult(response);
        });
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(() => client.GetAsync(Guid.NewGuid()));

        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task FalhaDeRedeDepoisDasTentativas_ViraTimeoutComCausaSanitizada()
    {
        var inner = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("falha ao conectar em https://segredo-host.exemplo.com com Authorization Bearer abc", new IOException("reset por peer")));
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));

        var exception = await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.GetAsync(Guid.NewGuid()));

        Assert.Equal(3, inner.CallCount);
        Assert.Contains("rede", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(exception.InnerException);
        Assert.IsNotType<HttpRequestException>(exception.InnerException);
        Assert.DoesNotContain("segredo-host", exception.InnerException!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", exception.InnerException.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaDeRedeNaGeracaoDoToken_ViraTimeout_NaoAuthenticationException()
    {
        var handler = new FakeHttpMessageHandler((request, _) =>
            IsToken(request) ? throw new HttpRequestException("dns") : Task.FromResult(TestApiClientFactory.Json(CustomerJson)));
        using var provider = Build(handler);
        var request = new CreateCustomerRequest("12345678909", "Maria", "Souza", "m@x.com", "+55 (11) 99876-5432", DateTimeOffset.UtcNow.AddYears(-30));

        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => provider.GetRequiredService<ICredicarroClient>().Customers.CreateAsync(request));
    }

    [Fact]
    public async Task CircuitBreaker_ExcecaoNaMeiaAbertura_NaoFicaPreso()
    {
        var options = Options(r =>
        {
            r.MaxRetryAttempts = 1;
            r.CircuitBreakerMinimumThroughput = 2;
            r.CircuitBreakerFailureRatio = 0.5;
            r.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);
            r.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500);
        });
        var mode = "falha";
        var inner = new FakeHttpMessageHandler((_, _) => mode switch
        {
            "falha" => Task.FromResult(TestApiClientFactory.Status(503)),
            "excecao" => throw new InvalidOperationException("boom"),
            _ => Task.FromResult(TestApiClientFactory.Json("""{ "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9" }"""))
        });
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner, options));

        await Assert.ThrowsAsync<CredicarroApiException>(() => client.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<CredicarroApiException>(() => client.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<CredicarroTimeoutException>(() => client.GetAsync(Guid.NewGuid()));

        await Task.Delay(800);
        mode = "excecao";
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(Guid.NewGuid()));

        mode = "ok";
        var simulation = await client.GetAsync(Guid.NewGuid());

        Assert.NotNull(simulation);
    }

    [Fact]
    public async Task Upload_ReusaOMesmoArrayEmTodasAsTentativasENoReplayDo401()
    {
        var arrays = new List<byte[]>();
        var inner = new FakeHttpMessageHandler((request, callIndex) =>
        {
            lock (arrays)
                arrays.Add(((BufferedContent)request.Content!).Bytes);

            return Task.FromResult(TestApiClientFactory.Status(callIndex switch { 1 => 502, 2 => 401, _ => 204 }));
        });
        var client = new ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner));

        await client.UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()));

        Assert.True(arrays.Count >= 3);
        Assert.All(arrays, array => Assert.Same(arrays[0], array));
    }

    [Fact]
    public async Task Upload_ComArquivoGrande_EnviaOBase64CorretoSemQuebraDeLinha()
    {
        var content = new byte[2 * 1024 * 1024];
        new Random(42).NextBytes(content);
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));
        var client = new ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner));

        await client.UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, DocumentFile.FromBytes(content, "grande.pdf")));

        using var body = System.Text.Json.JsonDocument.Parse(inner.Requests.Single().Body);
        Assert.Equal(Convert.ToBase64String(content), body.RootElement.GetProperty("conteudoBase64").GetString());
    }

    private sealed class EndlessStream : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            BytesRead += count;
            return count;
        }
    }

    [Fact]
    public void DocumentFile_FromStream_NuncaConfiaEmLength_ELeNoMaximoMaisUmByteDoLimite()
    {
        using var stream = new EndlessStream();

        Assert.Throws<ArgumentException>(() => DocumentFile.FromStream(stream, "infinito.pdf"));

        Assert.True(stream.BytesRead <= DocumentFile.MaxSizeInBytes + 1, $"Leu {stream.BytesRead} bytes.");
    }

    [Fact]
    public void DocumentFile_FromFile_LeEmLacoEValidaOTamanhoReal()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllBytes(path, new byte[DocumentFile.MaxSizeInBytes + 1]);

            Assert.Throws<ArgumentException>(() => DocumentFile.FromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BufferedContent_Clone_CompartilhaOArraySemCopiar()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://x.local/v1/customer")
        {
            Content = new BufferedContent(new byte[] { 1, 2, 3 })
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");

        var clone = request.Clone();

        Assert.Same(((BufferedContent)request.Content).Bytes, ((BufferedContent)clone.Content!).Bytes);
        Assert.Equal("application/json", clone.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Customers_CreateAddressEProfessional_AceitamIdempotencyKeyExplicita()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(CustomerJson)));
        ICustomersClient client = new CustomersClient(TestApiClientFactory.CreateApiHttpClient(inner));
        var create = new CreateCustomerRequest("12345678909", "Maria", "Souza", "m@x.com", "+55 (11) 99876-5432", DateTimeOffset.UtcNow.AddYears(-30));

        await client.CreateAsync(create, "criar-1");
        await client.UpsertAddressAsync("12345678909", new CustomerAddress("r", "b", "c", "SP", "01311000"), "end-1");
        await client.UpsertProfessionalDataAsync("12345678909", new ProfessionalData(OccupationType.Retired, 100m), "prof-1");

        Assert.Equal(["criar-1", "end-1", "prof-1"], inner.Requests.Select(request => request.Header("Idempotency-Key")!).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task IdempotencyKeyInvalida_LancaArgumentExceptionAntesDeQualquerRequisicao(string key)
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(CustomerJson)));
        ICustomersClient client = new CustomersClient(TestApiClientFactory.CreateApiHttpClient(inner));
        var create = new CreateCustomerRequest("12345678909", "Maria", "Souza", "m@x.com", "+55 (11) 99876-5432", DateTimeOffset.UtcNow.AddYears(-30));

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateAsync(create, key));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertAddressAsync("12345678909", new CustomerAddress("r", "b", "c", "SP", "01311000"), key));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertProfessionalDataAsync("12345678909", new ProfessionalData(OccupationType.Retired, 1m), key));
        await Assert.ThrowsAsync<ArgumentException>(() => new ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner))
            .UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()), key));

        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task IdempotencyKey_ComExatos200Caracteres_Funciona()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));
        var client = new ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner));

        await client.UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()), new string('k', 200));

        Assert.Equal(new string('k', 200), inner.Requests.Single().Header("Idempotency-Key"));
    }

    [Fact]
    public void ToString_DasRequisicoesERespostas_MascaraDadosPessoais()
    {
        var address = new CustomerAddress("Rua do Fulano", "Bairro X", "Cidade Y", "SP", "01311000", "1578", "Apto 12");
        var professional = new ProfessionalData(OccupationType.Employee, 654321.99m);
        var upsert = new UpsertCustomerRequest("12345678909", "Maria", "Souza", "maria.secreta@exemplo.com.br", "+55 (11) 99876-5432",
            new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.Zero), address, professional);
        var create = new CreateCustomerRequest("12345678909", "Maria", "Souza", "maria.secreta@exemplo.com.br", "+55 (11) 99876-5432", DateTimeOffset.UtcNow);
        var deposit = new DepositAccount("12345678909", "001", "1234", "5", "987654", "7", DepositAccountType.Checking);
        var vehicle = new VehicleDetails("98765432100", "9BWZZZ377VT004251", "ABC1D23", "PRATA");
        var proposalRequest = new CreateProposalRequest("12345678909", Guid.NewGuid());
        var financing = new CreateFinancingSimulationRequest("12345678909", "ABC1D23", 45000m, 5000m, "SP", false, "11222333000181");
        var refinancing = new CreateRefinancingSimulationRequest("12345678909", "ABC1D23", 25000m, "11222333000181");
        var financingV1 = new CreateFinancingSimulationV1Request("12345678909", "ABC1D23", 45000m, 5000m, "SP", false);
        var refinancingV1 = new CreateRefinancingSimulationV1Request("12345678909", "ABC1D23", 25000m);
        var upload = new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, DocumentFile.FromBytes(Encoding.UTF8.GetBytes("CONTEUDO-SECRETO"), "cnh-da-maria.pdf"));
        var participant = new ProposalParticipantState("12345678909", null, null, null, null, [], [new ProposalDocument(DocumentType.Cnh, 1, "CNH", DocumentStatus.Invalid, 2, "INVALIDO", "Ilegível: Maria Souza")]);
        var details = new ProposalDetails(ProposalId, 1, Guid.NewGuid(), ProposalStatus.Refused, 3, "RECUSADA", ProposalType.Financing, null,
            "Restrição da Maria", "https://onboarding.exemplo.com/sessao/abc123", participant, participant, participant);
        var summary = new ProposalSummary(ProposalId, 1, Guid.NewGuid(), ProposalType.Financing, ProposalStatus.Approved, "Approved", "123.456.789-01", "987.654.321-00", null);
        var simulation = new Simulation(Guid.NewGuid(), 12, 1m, 1m, 1m, 1m, 1m, 1m, 1m, "SP", SimulationType.Financing, null, null, null, null,
            new SimulationVehicle("Honda", "Civic", "Turing", 2024, 1m, "MWO-3910", false, "1Y5Ye4U8kydsr4828", "70502561697", "Prata"), "12345678909", null);

        var all = new object[] { upsert, create, address, professional, deposit, vehicle, proposalRequest, financing, refinancing, financingV1, refinancingV1, upload, participant, details, summary, simulation, simulation.Vehicle! };
        var forbidden = new[]
        {
            "12345678909", "123.456.789-01", "987.654.321-00", "Maria", "Souza", "maria.secreta", "99876", "654321", "Rua do Fulano", "01311000", "1578",
            "987654", "1234", "98765432100", "9BWZZZ", "CONTEUDO-SECRETO", "cnh-da-maria", "Ilegível", "Restrição", "abc123", "MWO-3910", "70502561697", "1Y5Ye4U8"
        };

        foreach (var item in all)
        {
            var text = item.ToString()!;

            foreach (var value in forbidden)
                Assert.False(text.Contains(value, StringComparison.Ordinal), $"{item.GetType().Name}.ToString() expôs '{value}': {text}");
        }

        Assert.Contains("***.456.789-**", upsert.ToString(), StringComparison.Ordinal);
        Assert.Contains("***.456.789-**", summary.ToString(), StringComparison.Ordinal);
        Assert.Contains("***.456.789-**", proposalRequest.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_DasExcecoes_NaoExpoeDetalheNemCorpo()
    {
        var problem = ProblemDetailsModel.Parse("""{ "tipo": "Validation.InvalidFormat", "erros": [ { "codigo": "X", "mensagem": "valor 12345678909 inválido", "campo": "cpf" } ] }""");
        var exception = new CredicarroApiException("A API do Credicarro retornou 400 (Validation.InvalidFormat).", HttpStatusCode.BadRequest, problem, "corpo com 12345678909");

        Assert.DoesNotContain("12345678909", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("12345678909", problem.Errors[0].ToString(), StringComparison.Ordinal);
    }
}

internal static class CycleTestExtensions
{
    public static IServiceCollection AddCredicarroForTests(this IServiceCollection services)
    {
        services.AddCredicarro(options =>
        {
            options.ClientId = "id";
            options.ClientSecret = "secret";
        });

        return services;
    }
}
