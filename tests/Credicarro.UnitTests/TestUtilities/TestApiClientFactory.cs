using System.Net;
using System.Text;
using Credicarro.Authentication;
using Credicarro.Configuration;
using Credicarro.Idempotency;
using Credicarro.Resilience;

namespace Credicarro.UnitTests.TestUtilities;

internal static class TestApiClientFactory
{
    private sealed class FakeTokenProvider : IAccessTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult("fake-token");

        public void Invalidate(string rejectedToken)
        {
        }
    }

    /// <summary>
    /// Opções com resiliência rápida para os testes (backoff de 1 ms).
    /// </summary>
    public static CredicarroOptions FastOptions()
    {
        var options = new CredicarroOptions { ClientId = "id", ClientSecret = "secret" };
        options.Resilience.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
        options.Resilience.TotalTimeout = TimeSpan.FromSeconds(10);
        options.Resilience.AttemptTimeout = TimeSpan.FromSeconds(5);
        return options;
    }

    /// <summary>
    /// Monta a mesma ordem de handlers do DI: idempotência (externa) -> resiliência -> autenticação -> fake.
    /// </summary>
    public static HttpClient CreateApiHttpClient(FakeHttpMessageHandler inner, CredicarroOptions? options = null)
    {
        options ??= FastOptions();

        var authHandler = new AuthenticationDelegatingHandler(new FakeTokenProvider()) { InnerHandler = inner };
        var resilienceHandler = new ResilienceDelegatingHandler(ResiliencePipelineFactory.Create(options.Resilience)) { InnerHandler = authHandler };
        var idempotencyHandler = new IdempotencyKeyDelegatingHandler(options) { InnerHandler = resilienceHandler };

        return new HttpClient(idempotencyHandler) { BaseAddress = options.ResolveBaseUrl() };
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(int status) => new((HttpStatusCode)status);
}
