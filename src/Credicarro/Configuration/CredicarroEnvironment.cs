namespace Credicarro.Configuration;

/// <summary>
/// Ambiente da API do Credicarro a ser utilizado pelo client.
/// </summary>
public enum CredicarroEnvironment
{
    /// <summary>
    /// Ambiente de staging (<c>https://api.staging.credicarro.com.br/integration/</c>).
    /// </summary>
    Staging,

    /// <summary>
    /// Ambiente de produção (<c>https://api.credicarro.com.br/integration/</c>).
    /// </summary>
    Production
}

internal static class CredicarroEnvironmentExtensions
{
    private const string StagingBaseUrl = "https://api.staging.credicarro.com.br/integration/";
    private const string ProductionBaseUrl = "https://api.credicarro.com.br/integration/";

    public static Uri ToBaseUri(this CredicarroEnvironment environment) => environment switch
    {
        CredicarroEnvironment.Staging => new Uri(StagingBaseUrl, UriKind.Absolute),
        CredicarroEnvironment.Production => new Uri(ProductionBaseUrl, UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente do Credicarro desconhecido.")
    };
}
