using System.Net;
using System.Text.Json;
using Credicarro.Authentication;
using Credicarro.Authentication.Models;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Authentication;

public class TokenApiClientTests
{
    private static TokenApiClient CreateClient(FakeHttpMessageHandler inner) =>
        new(new HttpClient(inner) { BaseAddress = new Uri("https://api.staging.credicarro.com.br/integration/") });

    [Fact]
    public async Task GenerateAsync_EnviaAsCredenciaisELeAResposta()
    {
        const string json = """
            {
                "token_de_acesso": "eyJ.token",
                "expira_em_segundos": 3600,
                "expira_em": "2026-10-19T13:59:00.000+00:00",
                "tipo_de_token": "Bearer",
                "escopo": "credicarro.integration.access/full"
            }
            """;
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(json)));

        var response = await CreateClient(inner).GenerateAsync("meu-id", "meu-segredo", CancellationToken.None);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v1/authentication/accesstoken", request.Path);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("meu-id", body.RootElement.GetProperty("clientId").GetString());
        Assert.Equal("meu-segredo", body.RootElement.GetProperty("clientSecret").GetString());

        Assert.Equal("eyJ.token", response.AccessToken);
        Assert.Equal(3600, response.ExpiresInSeconds);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 13, 59, 0, TimeSpan.Zero), response.ExpiresAt);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal("credicarro.integration.access/full", response.Scope);
    }

    [Fact]
    public async Task GenerateAsync_ComCredenciaisInvalidas401_LancaAuthenticationExceptionComCodigo()
    {
        const string json = """
            {
                "tipo": "Integration.Authentication.InvalidCredentials",
                "titulo": "Credenciais inválidas",
                "status": 401,
                "detalhe": "clientId ou clientSecret inválidos."
            }
            """;
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(json, HttpStatusCode.Unauthorized)));

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => CreateClient(inner).GenerateAsync("meu-id", "meu-segredo", CancellationToken.None));

        Assert.Equal("Integration.Authentication.InvalidCredentials", exception.ErrorCode);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_Com400_LancaAuthenticationException()
    {
        const string json = """{ "tipo": "Validation.Field.Required", "titulo": "Requisição inválida", "status": 400, "campo": "clientSecret" }""";
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json(json, HttpStatusCode.BadRequest)));

        var exception = await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => CreateClient(inner).GenerateAsync("meu-id", "", CancellationToken.None));

        Assert.Equal("Validation.Field.Required", exception.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_Com503_LancaApiException()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(503)));

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).GenerateAsync("meu-id", "meu-segredo", CancellationToken.None));

        Assert.Equal((HttpStatusCode)503, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_ComRespostaSemToken_LancaAuthenticationException()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Json("{}")));

        await Assert.ThrowsAsync<CredicarroAuthenticationException>(
            () => CreateClient(inner).GenerateAsync("meu-id", "meu-segredo", CancellationToken.None));
    }

    [Fact]
    public void GenerateAccessTokenRequest_ToString_NaoExpoeOClientSecret()
    {
        var request = new GenerateAccessTokenRequest("meu-id", "super-segredo");

        var text = request.ToString();

        Assert.DoesNotContain("super-segredo", text, StringComparison.Ordinal);
        Assert.Contains("meu-id", text, StringComparison.Ordinal);
    }
}
