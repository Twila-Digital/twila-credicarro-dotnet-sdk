using System.Net;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Simulations;
using Credicarro.Simulations.Models;
using Credicarro.UnitTests.TestUtilities;
using Credicarro.Webhooks;
using Credicarro.Webhooks.Models;

namespace Credicarro.UnitTests.Webhooks;

public class WebhookDeliveriesTests
{
    private static readonly Guid AuditId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly Guid WebhookId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private const string OptionJson = """
        { "simulacoes": [ { "id_simulacao": "123e4567-e89b-12d3-a456-426614174000", "valor_solicitado": 40000, "prazo": 36,
          "valor_parcela": 2042.42, "valor_tac": 1500, "valor_boleto": 3.5, "valor_seguro": 0, "valor_iof": 872, "percentual_iof": 1.92,
          "valor_credito_total": 42722, "valor_divida_total": 73527.12, "percentual_cet_mensal": 3.62, "percentual_cet_anual": 53.25,
          "percentual_juros_mensal": 3.29, "percentual_juros_anual": 47.53 } ] }
        """;

    private static IWebhooksClient CreateWebhooks(FakeHttpMessageHandler inner) =>
        new WebhooksClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static ISimulationsClient CreateSimulations(FakeHttpMessageHandler inner) =>
        new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    private static Dictionary<string, string?> Query(string query) =>
        query.TrimStart('?')
            .Split(['&'], StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(['='], 2))
            .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => (string?)Uri.UnescapeDataString(parts[1]));

    [Fact]
    public async Task ListDeliveriesAsync_EnviaFiltrosEPaginacao_EMapeiaOsEnvios()
    {
        var inner = RespondWith($$"""
            {
              "itens": [
                { "id_auditoria": "{{AuditId}}", "id_webhook": "{{WebhookId}}", "tipo": "Proposal", "requisicao": "{\"id_proposta\":\"x\"}",
                  "resposta": "ok", "status_http": 200, "data_criacao": "2026-11-05T14:32:10Z" },
                { "id_auditoria": "{{AuditId}}", "id_webhook": null, "tipo": "Algo", "requisicao": "{}", "resposta": "",
                  "status_http": null, "data_criacao": "2026-11-05T14:33:00Z" }
              ],
              "pagina": { "tem_proximo": true, "tem_anterior": false, "numero": 2, "tamanho": 50, "total": 51 }
            }
            """);

        var page = await CreateWebhooks(inner).ListDeliveriesAsync(
            new WebhookDeliveryFilter(
                InitialDate: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                ProposalNumber: "12345",
                StatusCode: 500),
            new ListRequest(2, 50));

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/webhook/audit", request.Path);
        var query = Query(request.Uri.Query);
        Assert.Equal("2", query["pagina_inicial"]);
        Assert.Equal("50", query["limite"]);
        Assert.Equal("2026-11-01T00:00:00.0000000-03:00", query["data_inicial"]);
        Assert.Equal("12345", query["numero_proposta"]);
        Assert.Equal("500", query["status_http"]);
        Assert.False(query.ContainsKey("data_final"));

        var first = page.Items[0];
        Assert.Equal(AuditId, first.Id);
        Assert.Equal(WebhookId, first.WebhookId);
        Assert.Equal(WebhookType.Proposal, first.Type);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal("ok", first.Response);
        Assert.DoesNotContain("id_proposta", first.ToString(), StringComparison.Ordinal);

        var second = page.Items[1];
        Assert.Null(second.WebhookId);
        Assert.Equal(WebhookType.Unknown, second.Type);
        Assert.Null(second.StatusCode);
        Assert.True(page.HasNext);
    }

    [Fact]
    public async Task ListDeliveriesAsync_SemFiltros_EnviaSoAPaginacao()
    {
        var inner = RespondWith("""{ "itens": [], "pagina": { "numero": 1, "tamanho": 10, "total": 0 } }""");

        await CreateWebhooks(inner).ListDeliveriesAsync();

        Assert.Equal("?pagina_inicial=1&limite=10", inner.Requests.Single().Uri.Query);
    }

    [Fact]
    public async Task ResendAsync_EnviaPostSemCorpoESemIdempotencyKey_EDevolveOResultado()
    {
        var inner = RespondWith("""{ "status_http": null, "resposta": "" }""");

        var result = await CreateWebhooks(inner).ResendAsync(AuditId);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/integration/v1/webhook/audit/{AuditId}/resend", request.Path);
        Assert.Equal(string.Empty, request.Body);
        Assert.False(request.HasHeader("Idempotency-Key"));
        Assert.Null(result.StatusCode);
        Assert.Equal(string.Empty, result.Response);
    }

    [Fact]
    public async Task ResendAsync_ComFalhaTransitoria_NaoRepeteParaNaoDispararDeNovo()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(
            """{ "tipo": "Organization.Webhook.Resend.DependencyUnavailable", "titulo": "Indisponível", "status": 503 }""",
            HttpStatusCode.ServiceUnavailable)));

        var error = await Assert.ThrowsAsync<CredicarroApiException>(() => CreateWebhooks(inner).ResendAsync(AuditId));

        Assert.Equal(1, inner.CallCount);
        Assert.Equal("Organization.Webhook.Resend.DependencyUnavailable", error.ErrorCode);
    }

    [Fact]
    public async Task GetCommissionReturnsAsync_EnviaOCnpj_EMapeiaAsFaixas()
    {
        var inner = RespondWith("""
            { "retornos_comissao": [
                { "tipo": "Financing", "alvo": "Store", "retorno_minimo": 0, "retorno_maximo": 4, "permite_taxa_padrao": true },
                { "tipo": "Outro", "alvo": "Novo", "retorno_minimo": 1, "retorno_maximo": 3, "permite_taxa_padrao": false }
            ] }
            """);

        var returns = await CreateSimulations(inner).GetCommissionReturnsAsync("11.222.333/0001-81");

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v2/proposal/simulation/commission-returns", request.Path);
        Assert.Equal("11.222.333/0001-81", Query(request.Uri.Query)["cnpjLoja"]);
        Assert.Equal(
            [
                new CommissionReturn(SimulationType.Financing, CommissionReturnTarget.Store, 0, 4, true),
                new CommissionReturn(SimulationType.Unknown, CommissionReturnTarget.Unknown, 1, 3, false)
            ],
            returns);
    }

    [Fact]
    public async Task Simulacoes_EnviamRetornoComissaoSoQuandoInformado()
    {
        var inner = RespondWith(OptionJson, HttpStatusCode.Created);
        var client = CreateSimulations(inner);

        await client.CreateFinancingAsync(new CreateFinancingSimulationRequest("12345678909", "ABC1D23", 45000, 5000, "SP", false, "11222333000181", 2));
        await client.CreateRefinancingAsync(new CreateRefinancingSimulationRequest("12345678909", "ABC1D23", 25000, "11222333000181", 0));
        await client.CreateRefinancingAsync(new CreateRefinancingSimulationRequest("12345678909", "ABC1D23", 25000, "11222333000181"));

        using var financing = JsonDocument.Parse(inner.Requests[0].Body);
        using var refinancing = JsonDocument.Parse(inner.Requests[1].Body);
        using var withoutReturn = JsonDocument.Parse(inner.Requests[2].Body);
        Assert.Equal(2, financing.RootElement.GetProperty("retornoComissao").GetInt32());
        Assert.Equal(0, refinancing.RootElement.GetProperty("retornoComissao").GetInt32());
        Assert.False(withoutReturn.RootElement.TryGetProperty("retornoComissao", out _));
    }

    [Fact]
    public async Task CreateFinancingAsync_RetornoForaDaFaixa_LancaBusinessRuleException()
    {
        var inner = RespondWith(
            """{ "tipo": "Store.CommissionReturn.OutOfRange", "titulo": "Regra de negócio não atendida", "status": 422 }""",
            (HttpStatusCode)422);

        var error = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(
            () => CreateSimulations(inner).CreateFinancingAsync(
                new CreateFinancingSimulationRequest("12345678909", "ABC1D23", 45000, 5000, "SP", false, "11222333000181", 9)));

        Assert.Equal("Store.CommissionReturn.OutOfRange", error.ErrorCode);
    }
}
