using Credicarro.Configuration;

namespace Credicarro.UnitTests.Configuration;

public class CredicarroOptionsValidatorTests
{
    private readonly CredicarroOptionsValidator _validator = new();

    private static CredicarroOptions Valid() => new() { ClientId = "id", ClientSecret = "secret" };

    [Fact]
    public void Validate_ComOpcoesValidas_Sucesso()
    {
        Assert.True(_validator.Validate(null, Valid()).Succeeded);
    }

    [Theory]
    [InlineData("", "secret", "ClientId")]
    [InlineData("id", "", "ClientSecret")]
    [InlineData("  ", "secret", "ClientId")]
    public void Validate_SemCredenciais_FalhaCitandoOCampo(string clientId, string clientSecret, string expectedField)
    {
        var options = new CredicarroOptions { ClientId = clientId, ClientSecret = clientSecret };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(expectedField, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://api.credicarro.com.br/integration")]
    [InlineData("ftp://api.credicarro.com.br/integration")]
    [InlineData("http://exemplo.local/integration")]
    public void Validate_ComBaseUrlSemHttps_Falha(string url)
    {
        var options = Valid();
        options.BaseUrl = new Uri(url);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("HTTPS", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://mock.local/integration")]
    [InlineData("http://localhost:5000/integration")]
    [InlineData("http://127.0.0.1:8080/integration")]
    public void Validate_ComHttpsOuLoopback_Sucesso(string url)
    {
        var options = Valid();
        options.BaseUrl = new Uri(url);

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_ComBaseUrlRelativa_Falha()
    {
        var options = Valid();
        options.BaseUrl = new Uri("/integration", UriKind.Relative);

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_ComAttemptTimeoutMaiorQueOTotal_Falha()
    {
        var options = Valid();
        options.Resilience.TotalTimeout = TimeSpan.FromSeconds(5);
        options.Resilience.AttemptTimeout = TimeSpan.FromSeconds(10);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("AttemptTimeout", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ComMaxRetryAttemptsZero_Falha()
    {
        var options = Valid();
        options.Resilience.MaxRetryAttempts = 0;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_ComDocumentUploadAttemptTimeoutZero_Falha()
    {
        var options = Valid();
        options.Resilience.DocumentUploadAttemptTimeout = TimeSpan.Zero;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Validate_ComFailureRatioForaDoIntervalo_Falha(double ratio)
    {
        var options = Valid();
        options.Resilience.CircuitBreakerFailureRatio = ratio;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_NuncaIncluiOSegredoNaMensagem()
    {
        var options = new CredicarroOptions { ClientId = "id", ClientSecret = "super-segredo", BaseUrl = new Uri("http://exemplo.local/x") };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.DoesNotContain("super-segredo", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ComBreakDurationAbaixoDoMinimoDoPolly_Falha()
    {
        var options = Valid();
        options.Resilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(200);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("CircuitBreakerBreakDuration", result.FailureMessage, StringComparison.Ordinal);
    }
}
