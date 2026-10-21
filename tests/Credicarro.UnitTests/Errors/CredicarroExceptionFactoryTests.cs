using System.Net;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Errors;

public class CredicarroExceptionFactoryTests
{
    private static Task<CredicarroException> Map(HttpResponseMessage response) =>
        CredicarroExceptionFactory.FromResponseAsync(response, CancellationToken.None);

    [Fact]
    public async Task FromResponse_Com400_RetornaValidationException()
    {
        const string json = """{ "tipo": "Validation.Cpf.Invalid", "titulo": "Requisição inválida", "status": 400, "detalhe": "O CPF informado em 'cpf' é inválido.", "campo": "cpf" }""";

        var exception = await Map(TestApiClientFactory.Json(json, HttpStatusCode.BadRequest));

        var validation = Assert.IsType<CredicarroValidationException>(exception);
        Assert.IsAssignableFrom<CredicarroApiException>(validation);
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.Equal("Validation.Cpf.Invalid", validation.ErrorCode);
        Assert.Equal("Requisição inválida", validation.Title);
        Assert.Equal("cpf", validation.Field);
        Assert.Equal("O CPF informado em 'cpf' é inválido.", validation.Detail);
    }

    [Fact]
    public async Task FromResponse_Com422_RetornaBusinessRuleException()
    {
        const string json = """{ "tipo": "Store.Integration.Intermediation", "titulo": "Regra de negócio não atendida", "status": 422, "detalhe": "A loja possui contrato de intermediação." }""";

        var exception = await Map(TestApiClientFactory.Json(json, (HttpStatusCode)422));

        var businessRule = Assert.IsType<CredicarroBusinessRuleException>(exception);
        Assert.Equal((HttpStatusCode)422, businessRule.StatusCode);
        Assert.Equal("Store.Integration.Intermediation", businessRule.ErrorCode);
    }

    [Fact]
    public async Task FromResponse_Com429_RetornaRateLimitExceptionComRetryAfter()
    {
        var response = TestApiClientFactory.Json("""{ "tipo": "RateLimit.Exceeded", "titulo": "Muitas requisições" }""", (HttpStatusCode)429);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        var exception = await Map(response);

        var rateLimit = Assert.IsType<CredicarroRateLimitException>(exception);
        Assert.Equal(TimeSpan.FromSeconds(7), rateLimit.RetryAfter);
        Assert.Equal((HttpStatusCode)429, rateLimit.StatusCode);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task FromResponse_ComOutrosStatus_RetornaApiException(int status)
    {
        var exception = await Map(TestApiClientFactory.Json("""{ "tipo": "Algum.Codigo.Estavel", "titulo": "Titulo" }""", (HttpStatusCode)status));

        var api = Assert.IsType<CredicarroApiException>(exception);
        Assert.Equal((HttpStatusCode)status, api.StatusCode);
        Assert.Equal("Algum.Codigo.Estavel", api.ErrorCode);
    }

    [Fact]
    public async Task FromResponse_Com401SemCorpo_RetornaAuthenticationException()
    {
        var exception = await Map(TestApiClientFactory.Status(401));

        var authentication = Assert.IsType<CredicarroAuthenticationException>(exception);
        Assert.Equal(HttpStatusCode.Unauthorized, authentication.StatusCode);
        Assert.Null(authentication.ErrorCode);
        Assert.Contains("401", authentication.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromResponse_Com500NoFormatoPadrao_UsaFallbackTypeTitleDetail()
    {
        const string json = """{ "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1", "title": "Erro interno", "status": 500, "detail": "boom" }""";

        var exception = await Map(TestApiClientFactory.Json(json, HttpStatusCode.InternalServerError));

        var api = Assert.IsType<CredicarroApiException>(exception);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.6.1", api.ErrorCode);
        Assert.Equal("Erro interno", api.Title);
        Assert.Equal("boom", api.Detail);
    }

    [Fact]
    public async Task FromResponse_ComCorpoQueNaoEJson_NaoLanca()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Bad gateway</html>") };

        var exception = await Map(response);

        var api = Assert.IsType<CredicarroApiException>(exception);
        Assert.Null(api.ErrorCode);
        Assert.Equal("<html>Bad gateway</html>", api.RawBody);
    }

    [Fact]
    public async Task FromResponse_MensagemTrazSoStatusTipoETitulo_SemDetalheNemCorpo()
    {
        const string json = """{ "tipo": "Customer.Account.Incomplete", "titulo": "Regra de negócio não atendida", "status": 422, "detalhe": "CPF 12345678909 sem endereço." }""";

        var exception = await Map(TestApiClientFactory.Json(json, (HttpStatusCode)422));

        Assert.Equal("A API do Credicarro retornou 422 (Customer.Account.Incomplete): Regra de negócio não atendida.", exception.Message);
        Assert.DoesNotContain("12345678909", exception.Message, StringComparison.Ordinal);
        Assert.Contains("12345678909", ((CredicarroApiException)exception).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromResponse_ExpoeAListaDeErrosEOCorpoBruto()
    {
        const string json = """{ "tipo": "Validation.InvalidFormat", "erros": [ { "codigo": "Validation.InvalidFormat", "mensagem": "m", "campo": "valorVeiculo" } ] }""";

        var exception = await Map(TestApiClientFactory.Json(json, HttpStatusCode.BadRequest));

        var api = Assert.IsAssignableFrom<CredicarroApiException>(exception);
        Assert.Single(api.Errors);
        Assert.Equal("valorVeiculo", api.Errors[0].Field);
        Assert.Equal(json, api.RawBody);
    }

    [Fact]
    public async Task FromTokenResponse_Com400_ViraAuthenticationException()
    {
        var exception = await CredicarroExceptionFactory.FromTokenResponseAsync(
            TestApiClientFactory.Json("""{ "tipo": "Validation.Field.Required", "campo": "clientId" }""", HttpStatusCode.BadRequest),
            CancellationToken.None);

        var authentication = Assert.IsType<CredicarroAuthenticationException>(exception);
        Assert.Equal("Validation.Field.Required", authentication.ErrorCode);
    }

    [Fact]
    public void Hierarquia_SegueOContrato()
    {
        Assert.True(typeof(CredicarroConfigurationException).IsSubclassOf(typeof(CredicarroException)));
        Assert.True(typeof(CredicarroAuthenticationException).IsSubclassOf(typeof(CredicarroException)));
        Assert.True(typeof(CredicarroApiException).IsSubclassOf(typeof(CredicarroException)));
        Assert.True(typeof(CredicarroValidationException).IsSubclassOf(typeof(CredicarroApiException)));
        Assert.True(typeof(CredicarroBusinessRuleException).IsSubclassOf(typeof(CredicarroApiException)));
        Assert.True(typeof(CredicarroRateLimitException).IsSubclassOf(typeof(CredicarroApiException)));
        Assert.True(typeof(CredicarroTimeoutException).IsSubclassOf(typeof(CredicarroException)));
        Assert.True(typeof(CredicarroWebhookSignatureException).IsSubclassOf(typeof(CredicarroException)));
    }
}
