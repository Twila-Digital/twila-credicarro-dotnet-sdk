using System.Net;
using System.Text.Json;
using Credicarro.Authentication;
using Credicarro.Configuration;
using Credicarro.Customers.Models;
using Credicarro.Errors;
using Credicarro.Proposals.Models;
using Credicarro.Serialization;
using Credicarro.Simulations;
using Credicarro.UnitTests.TestUtilities;
using Credicarro.Webhooks.Models;

namespace Credicarro.UnitTests.Cycle2;

/// <summary>
/// Ciclo 2: erros do endpoint de token, serialização com valores reais (máscara só no ToString) e contexto do pool.
/// </summary>
public class Cycle2Tests
{
    private static TokenApiClient TokenClient(FakeHttpMessageHandler inner) =>
        new(new HttpClient(inner) { BaseAddress = new Uri("https://api.staging.credicarro.com.br/integration/") });

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    public async Task Token_400e401_ViramAuthenticationException(int status)
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(status)));

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => TokenClient(inner).GenerateAsync("id", "segredo", CancellationToken.None));

        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Token_3xxEDemaisStatus_ViramApiExceptionComStatusETipo(int status)
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            TestApiClientFactory.Json("""{ "tipo": "Algum.Codigo", "titulo": "Titulo" }""", (HttpStatusCode)status)));

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => TokenClient(inner).GenerateAsync("id", "segredo", CancellationToken.None));

        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Equal("Algum.Codigo", exception.ErrorCode);
    }

    [Fact]
    public async Task Token_422e429_ViramSubclassesDeApiException()
    {
        var unprocessable = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(422)));
        var limited = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(429)));

        await Assert.ThrowsAsync<CredicarroBusinessRuleException>(() => TokenClient(unprocessable).GenerateAsync("id", "s", CancellationToken.None));
        await Assert.ThrowsAsync<CredicarroRateLimitException>(() => TokenClient(limited).GenerateAsync("id", "s", CancellationToken.None));
    }

    private static readonly UpsertCustomerRequest Customer = new(
        "12345678909", "Maria", "Souza", "maria.souza@exemplo.com.br", "+55 (11) 99876-5432",
        new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
        new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000", "1578", "Bloco B"),
        new ProfessionalData(OccupationType.Employee, 6500.50m));

    [Fact]
    public void Serializacao_DosModelosDeRequisicao_CarregaOsValoresReaisEViraIda()
    {
        var json = JsonSerializer.Serialize(Customer);

        Assert.Contains("12345678909", json, StringComparison.Ordinal);
        Assert.Contains("maria.souza@exemplo.com.br", json, StringComparison.Ordinal);
        Assert.Contains("99876-5432", json, StringComparison.Ordinal);
        Assert.Contains("6500.50", json, StringComparison.Ordinal);
        Assert.DoesNotContain("***", json, StringComparison.Ordinal);

        Assert.Equal(Customer, JsonSerializer.Deserialize<UpsertCustomerRequest>(json));

        var deposit = new DepositAccount("12345678909", "001", "1234", "5", "987654", "7", DepositAccountType.Savings);
        Assert.Equal(deposit, JsonSerializer.Deserialize<DepositAccount>(JsonSerializer.Serialize(deposit)));

        var vehicle = new VehicleDetails("98765432100", "9BWZZZ377VT004251", "ABC1D23", "PRATA");
        Assert.Equal(vehicle, JsonSerializer.Deserialize<VehicleDetails>(JsonSerializer.Serialize(vehicle)));
    }

    [Fact]
    public void Serializacao_DasRespostas_CarregaOsValoresReais()
    {
        var participant = new ProposalParticipantState("12345678909", BiometryStatus.Approved, 3, "APROVADA", null, [],
            [new ProposalDocument(DocumentType.Cnh, 1, "CNH", DocumentStatus.Invalid, 2, "INVALIDO", "Documento ilegivel")]);
        var details = new ProposalDetails(Guid.NewGuid(), 1, Guid.NewGuid(), ProposalStatus.Refused, 3, "RECUSADA", ProposalType.Financing, null,
            "Restricao cadastral", "https://onboarding.exemplo.com/sessao/abc123", participant, null, null);
        var summary = new ProposalSummary(Guid.NewGuid(), 1, Guid.NewGuid(), ProposalType.Financing, ProposalStatus.Approved, "Approved", "123.456.789-01", null, null);

        var detailsJson = JsonSerializer.Serialize(details);
        var summaryJson = JsonSerializer.Serialize(summary);

        Assert.Contains("12345678909", detailsJson, StringComparison.Ordinal);
        Assert.Contains("Restricao cadastral", detailsJson, StringComparison.Ordinal);
        Assert.Contains("abc123", detailsJson, StringComparison.Ordinal);
        Assert.Contains("Documento ilegivel", detailsJson, StringComparison.Ordinal);
        Assert.Contains("123.456.789-01", summaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain("***", detailsJson + summaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Serializacao_DoResultadoDeCadastroDeWebhook_LevaOSegredoReal_EOToStringMascara()
    {
        var result = new CreateWebhookResult(Guid.NewGuid(), "segredo-real-de-assinatura");
        var request = new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/w", WebhookAuthenticationType.Basic, "credencial-real");

        Assert.Contains("segredo-real-de-assinatura", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.Contains("credencial-real", JsonSerializer.Serialize(request), StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-real-de-assinatura", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("credencial-real", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Serializacao_DasOpcoesDoClient_ERecusada()
    {
        var options = new CredicarroOptions { ClientId = "id", ClientSecret = "segredo-real" };

        var serialize = Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<CredicarroOptions>("""{"ClientId":"a","ClientSecret":"b"}"""));
        Assert.DoesNotContain("segredo-real", serialize.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serializacao_DasExcecoes_NaoCarregaDetalheNemCorpo()
    {
        var problem = ProblemDetailsModel.Parse("""{ "tipo": "T", "detalhe": "CPF 12345678909 invalido" }""");
        var exception = new CredicarroApiException("A API do Credicarro retornou 400 (T).", HttpStatusCode.BadRequest, problem, "corpo 12345678909");

        Assert.DoesNotContain("12345678909", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("12345678909", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChamadaCanceladaEnquantoEmVoo_NaoCorrompeAsChamadasSeguintes()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new FakeHttpMessageHandler(async (request, callIndex, _) =>
        {
            if (callIndex == 1)
                await release.Task;

            return TestApiClientFactory.Json("""{ "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9", "prazo": 12 }""");
        });
        var client = new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));
        using var cts = new CancellationTokenSource();

        var first = client.GetAsync(Guid.NewGuid(), cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.GetAsync(Guid.NewGuid())));
        Assert.All(results, simulation => Assert.Equal(12, simulation.Term));

        release.SetResult(true);
        await Task.Delay(100);

        var after = await client.GetAsync(Guid.NewGuid());
        Assert.Equal(12, after.Term);
    }
}
