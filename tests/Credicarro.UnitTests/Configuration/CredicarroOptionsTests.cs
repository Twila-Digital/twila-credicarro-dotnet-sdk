using Credicarro.Configuration;

namespace Credicarro.UnitTests.Configuration;

public class CredicarroOptionsTests
{
    [Fact]
    public void Environment_PadraoEProducao()
    {
        Assert.Equal(CredicarroEnvironment.Production, new CredicarroOptions().Environment);
    }

    [Fact]
    public void ResolveBaseUrl_Staging()
    {
        var options = new CredicarroOptions { Environment = CredicarroEnvironment.Staging };

        Assert.Equal("https://api.staging.credicarro.com.br/integration/", options.ResolveBaseUrl().ToString());
    }

    [Fact]
    public void ResolveBaseUrl_Production()
    {
        var options = new CredicarroOptions { Environment = CredicarroEnvironment.Production };

        Assert.Equal("https://api.credicarro.com.br/integration/", options.ResolveBaseUrl().ToString());
    }

    [Fact]
    public void ResolveBaseUrl_ComBaseUrlCustomizada_TemPrioridadeSobreOAmbiente()
    {
        var customUrl = new Uri("https://mock.local/integration/");
        var options = new CredicarroOptions { Environment = CredicarroEnvironment.Production, BaseUrl = customUrl };

        Assert.Equal(customUrl, options.ResolveBaseUrl());
    }

    [Fact]
    public void ResolveBaseUrl_SemBarraFinal_AdicionaABarra()
    {
        var options = new CredicarroOptions { BaseUrl = new Uri("https://mock.local/integration") };

        Assert.Equal("https://mock.local/integration/", options.ResolveBaseUrl().ToString());
    }

    [Theory]
    [InlineData(CredicarroEnvironment.Staging)]
    [InlineData(CredicarroEnvironment.Production)]
    public void ResolveBaseUrl_CombinadaComCaminhoRelativo_PreservaOSegmentoIntegration(CredicarroEnvironment environment)
    {
        var options = new CredicarroOptions { Environment = environment };

        var combined = new Uri(options.ResolveBaseUrl(), "v1/authentication/accesstoken");

        Assert.Contains("/integration/v1/authentication/accesstoken", combined.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NuncaIncluiOClientSecret()
    {
        var options = new CredicarroOptions { ClientId = "meu-id", ClientSecret = "super-segredo-do-cliente" };

        var text = options.ToString();

        Assert.DoesNotContain("super-segredo-do-cliente", text, StringComparison.Ordinal);
        Assert.Contains("meu-id", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Resilience_TemOsPadroesDoContrato()
    {
        var resilience = new CredicarroResilienceOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), resilience.TotalTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), resilience.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), resilience.DocumentUploadAttemptTimeout);
        Assert.Equal(3, resilience.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(500), resilience.RetryBaseDelay);
        Assert.Equal(0.5, resilience.CircuitBreakerFailureRatio);
        Assert.Equal(TimeSpan.FromSeconds(30), resilience.CircuitBreakerSamplingDuration);
        Assert.Equal(10, resilience.CircuitBreakerMinimumThroughput);
        Assert.Equal(TimeSpan.FromSeconds(15), resilience.CircuitBreakerBreakDuration);
        Assert.True(resilience.RetryOn500);
        Assert.False(resilience.DisableAutomaticIdempotencyKey);
    }
}
