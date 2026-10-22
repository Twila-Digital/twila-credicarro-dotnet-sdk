using System.Net;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;
using Credicarro.Webhooks;
using Credicarro.Webhooks.Models;

namespace Credicarro.UnitTests.Webhooks;

public class WebhooksClientTests
{
    private static readonly Guid WebhookId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private static IWebhooksClient CreateClient(FakeHttpMessageHandler inner) =>
        new WebhooksClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    [Fact]
    public async Task CreateAsync_EnviaOCadastroSemIdempotencyKey_EDevolveOSegredoDeAssinatura()
    {
        var inner = RespondWith($$"""{ "id_webhook": "{{WebhookId}}", "segredo_assinatura": "segredo-so-aparece-agora" }""", HttpStatusCode.Created);

        var result = await CreateClient(inner).CreateAsync(
            new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/webhooks/credicarro", WebhookAuthenticationType.Basic, "dXN1YXJpbzpzZW5oYQ=="));

        Assert.Equal(WebhookId, result.WebhookId);
        Assert.Equal("segredo-so-aparece-agora", result.SigningSecret);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v1/webhook", request.Path);
        Assert.False(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(3, body.RootElement.GetProperty("tipo").GetInt32());
        Assert.Equal("https://parceiro.com.br/webhooks/credicarro", body.RootElement.GetProperty("url").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("tipo_autenticacao").GetInt32());
        Assert.Equal("dXN1YXJpbzpzZW5oYQ==", body.RootElement.GetProperty("credencial").GetString());
    }

    [Fact]
    public async Task CreateAsync_SemAutenticacao_NaoEnviaCredencial()
    {
        var inner = RespondWith($$"""{ "id_webhook": "{{WebhookId}}", "segredo_assinatura": "s" }""", HttpStatusCode.Created);

        await CreateClient(inner).CreateAsync(new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/w", WebhookAuthenticationType.None));

        using var body = JsonDocument.Parse(inner.Requests.Single().Body);
        Assert.False(body.RootElement.TryGetProperty("credencial", out _));
        Assert.Equal(1, body.RootElement.GetProperty("tipo_autenticacao").GetInt32());
    }

    [Fact]
    public async Task CreateAsync_ComFalhaTransitoria_NaoRepeteParaNaoDuplicarOWebhook()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(503)));

        await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).CreateAsync(new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/w", WebhookAuthenticationType.None)));

        Assert.Equal(1, inner.CallCount);
    }

    [Theory]
    [InlineData(WebhookAuthenticationType.Basic)]
    [InlineData(WebhookAuthenticationType.Jwt)]
    public async Task CreateAsync_ComAutenticacaoSemCredencial_LancaArgumentException(WebhookAuthenticationType authentication)
    {
        var inner = RespondWith("{}");

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateClient(inner).CreateAsync(new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/w", authentication)));
    }

    [Fact]
    public async Task CreateAsync_ComTipoUnknown_LancaArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateClient(RespondWith("{}")).CreateAsync(new CreateWebhookRequest(WebhookType.Unknown, "https://parceiro.com.br/w", WebhookAuthenticationType.None)));
    }

    [Fact]
    public async Task CreateAsync_ComUrlRejeitada_LancaBusinessRuleException()
    {
        const string json = """{ "tipo": "Organization.Webhook.InvalidEndpoint", "titulo": "Regra de negócio não atendida", "status": 422 }""";
        var inner = RespondWith(json, (HttpStatusCode)422);

        var exception = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(
            () => CreateClient(inner).CreateAsync(new CreateWebhookRequest(WebhookType.Proposal, "https://localhost/w", WebhookAuthenticationType.None)));

        Assert.Equal("Organization.Webhook.InvalidEndpoint", exception.ErrorCode);
    }

    [Fact]
    public void CreateWebhookResult_EORequest_ToString_NaoExpoemSegredoNemCredencial()
    {
        var result = new CreateWebhookResult(WebhookId, "segredo-super-sensivel");
        var request = new CreateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/w", WebhookAuthenticationType.Basic, "credencial-sensivel");

        Assert.DoesNotContain("segredo-super-sensivel", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("credencial-sensivel", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_LeOsEnumsAPartirDoTextoEPaginacao()
    {
        const string json = """
            {
                "itens": [
                    { "id_webhook": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "tipo": "Proposal", "url": "https://parceiro.com.br/w", "tipo_autenticacao": "Basic", "status": "Active" },
                    { "id_webhook": "4fa85f64-5717-4562-b3fc-2c963f66afa6", "tipo": "Customer", "url": "https://parceiro.com.br/c", "tipo_autenticacao": "Jwt", "status": "Inactive" },
                    { "id_webhook": "5fa85f64-5717-4562-b3fc-2c963f66afa6", "tipo": "Futuro", "url": "https://parceiro.com.br/f", "tipo_autenticacao": "Mtls", "status": "Suspended" }
                ],
                "pagina": { "tem_proximo": false, "tem_anterior": false, "numero": 1, "tamanho": 10, "total": 3 }
            }
            """;
        var inner = RespondWith(json);

        var page = await CreateClient(inner).ListAsync();

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/webhook", request.Path);
        Assert.Equal("?pagina_inicial=1&limite=10", request.Uri.Query);

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(new Webhook(WebhookId, WebhookType.Proposal, "https://parceiro.com.br/w", WebhookAuthenticationType.Basic, WebhookStatus.Active), page.Items[0]);
        Assert.Equal(WebhookType.Customer, page.Items[1].Type);
        Assert.Equal(WebhookAuthenticationType.Jwt, page.Items[1].AuthenticationType);
        Assert.Equal(WebhookStatus.Inactive, page.Items[1].Status);
        Assert.Equal(WebhookType.Unknown, page.Items[2].Type);
        Assert.Equal(WebhookAuthenticationType.Unknown, page.Items[2].AuthenticationType);
        Assert.Equal(WebhookStatus.Unknown, page.Items[2].Status);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_ComPaginacaoPersonalizada_EnviaOsParametros()
    {
        var inner = RespondWith("""{ "itens": [], "pagina": { "numero": 4, "tamanho": 50, "total": 0 } }""");

        await CreateClient(inner).ListAsync(new ListRequest(4, 50));

        Assert.Equal("?pagina_inicial=4&limite=50", inner.Requests.Single().Uri.Query);
    }

    [Fact]
    public async Task UpdateAsync_EnviaTipoUrlEStatusNoCaminhoDoWebhook()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));

        await CreateClient(inner).UpdateAsync(WebhookId, new UpdateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/novo", WebhookStatus.Inactive));

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/integration/v1/webhook/3fa85f64-5717-4562-b3fc-2c963f66afa6", request.Path);
        Assert.False(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(3, body.RootElement.GetProperty("tipo").GetInt32());
        Assert.Equal("https://parceiro.com.br/novo", body.RootElement.GetProperty("url").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UpdateAsync_SemStatus_NaoEnviaOCampo()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));

        await CreateClient(inner).UpdateAsync(WebhookId, new UpdateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/novo"));

        using var body = JsonDocument.Parse(inner.Requests.Single().Body);
        Assert.False(body.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task UpdateAsync_ComFalhaTransitoria_Repete()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex == 1 ? 503 : 204)));

        await CreateClient(inner).UpdateAsync(WebhookId, new UpdateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/novo"));

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task UpdateAsync_ComWebhookInexistente_Lanca404()
    {
        const string json = """{ "tipo": "Webhook.Registration.NotFound", "titulo": "Não encontrado", "status": 404 }""";
        var inner = RespondWith(json, HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).UpdateAsync(WebhookId, new UpdateWebhookRequest(WebhookType.Proposal, "https://parceiro.com.br/novo")));

        Assert.Equal("Webhook.Registration.NotFound", exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task DeleteAsync_EnviaDeleteNoCaminhoDoWebhook()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));

        await CreateClient(inner).DeleteAsync(WebhookId);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/integration/v1/webhook/3fa85f64-5717-4562-b3fc-2c963f66afa6", request.Path);
        Assert.Equal(string.Empty, request.Body);
    }

    [Fact]
    public async Task DeleteAsync_ComConflitoDeConcorrencia_Lanca409()
    {
        const string json = """{ "tipo": "Organization.Webhook.ConcurrencyConflict", "titulo": "Conflito", "status": 409 }""";
        var inner = RespondWith(json, HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(() => CreateClient(inner).DeleteAsync(WebhookId));

        Assert.Equal("Organization.Webhook.ConcurrencyConflict", exception.ErrorCode);
    }
}
