using System.ComponentModel.DataAnnotations;

namespace Credicarro.Configuration;

/// <summary>
/// Opções de configuração do client do Credicarro.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.RefuseSerializationConverter<CredicarroOptions>))]
public sealed class CredicarroOptions
{
    /// <summary>
    /// Identificador do cliente (<c>clientId</c>), fornecido pelo Credicarro.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "ClientId é obrigatório.")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Segredo do cliente (<c>clientSecret</c>), fornecido pelo Credicarro.
    /// Nunca é incluído em logs, mensagens de exceção ou <see cref="object.ToString"/>.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "ClientSecret é obrigatório.")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Ambiente de destino das chamadas. Default: <see cref="CredicarroEnvironment.Production"/>.
    /// </summary>
    public CredicarroEnvironment Environment { get; set; } = CredicarroEnvironment.Production;

    /// <summary>
    /// URL base customizada. Quando definida, substitui a URL derivada de <see cref="Environment"/>.
    /// Precisa ser HTTPS (exceto <c>localhost</c> e <c>127.0.0.1</c>, para testes).
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Opções da pipeline de resiliência (timeout, retry, circuit breaker, idempotência).
    /// </summary>
    public CredicarroResilienceOptions Resilience { get; set; } = new();

    internal Uri ResolveBaseUrl()
    {
        var uri = BaseUrl ?? Environment.ToBaseUri();

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
    }

    /// <summary>
    /// Nunca inclui o <see cref="ClientSecret"/>.
    /// </summary>
    public override string ToString() =>
        $"CredicarroOptions {{ ClientId = {ClientId}, ClientSecret = ***, Environment = {Environment} }}";
}
