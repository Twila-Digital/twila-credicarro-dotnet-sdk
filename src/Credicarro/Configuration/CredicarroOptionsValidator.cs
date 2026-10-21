using Microsoft.Extensions.Options;

namespace Credicarro.Configuration;

/// <summary>
/// Valida <see cref="CredicarroOptions"/> além do que <see cref="System.ComponentModel.DataAnnotations"/> cobre.
/// As mensagens identificam o campo, nunca o valor.
/// </summary>
internal sealed class CredicarroOptionsValidator : IValidateOptions<CredicarroOptions>
{
    public ValidateOptionsResult Validate(string? name, CredicarroOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
            return ValidateOptionsResult.Fail("ClientId é obrigatório.");

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            return ValidateOptionsResult.Fail("ClientSecret é obrigatório.");

        if (!Enum.IsDefined(typeof(CredicarroEnvironment), options.Environment))
            return ValidateOptionsResult.Fail("Environment deve ser Staging ou Production.");

        if (options.BaseUrl is not null)
        {
            if (!options.BaseUrl.IsAbsoluteUri)
                return ValidateOptionsResult.Fail("BaseUrl, quando informada, deve ser uma URI absoluta.");

            if (!IsHttpsOrLoopback(options.BaseUrl))
                return ValidateOptionsResult.Fail("BaseUrl deve usar HTTPS (HTTP só é aceito para localhost e 127.0.0.1).");
        }

        var resilience = options.Resilience;

        if (resilience.MaxRetryAttempts < 1)
            return ValidateOptionsResult.Fail("Resilience.MaxRetryAttempts deve ser maior ou igual a 1.");

        if (resilience.TotalTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Resilience.TotalTimeout deve ser maior que zero.");

        if (resilience.AttemptTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Resilience.AttemptTimeout deve ser maior que zero.");

        if (resilience.AttemptTimeout > resilience.TotalTimeout)
            return ValidateOptionsResult.Fail("Resilience.AttemptTimeout não pode ser maior que Resilience.TotalTimeout.");

        if (resilience.DocumentUploadAttemptTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Resilience.DocumentUploadAttemptTimeout deve ser maior que zero.");

        if (resilience.RetryBaseDelay < TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Resilience.RetryBaseDelay não pode ser negativo.");

        if (resilience.CircuitBreakerFailureRatio is <= 0 or > 1)
            return ValidateOptionsResult.Fail("Resilience.CircuitBreakerFailureRatio deve estar entre 0 (exclusivo) e 1 (inclusivo).");

        if (resilience.CircuitBreakerBreakDuration < TimeSpan.FromMilliseconds(500))
            return ValidateOptionsResult.Fail("Resilience.CircuitBreakerBreakDuration deve ser de pelo menos 500 ms.");

        if (resilience.CircuitBreakerSamplingDuration < TimeSpan.FromSeconds(0.5))
            return ValidateOptionsResult.Fail("Resilience.CircuitBreakerSamplingDuration deve ser de pelo menos 500 ms.");

        if (resilience.CircuitBreakerMinimumThroughput < 2)
            return ValidateOptionsResult.Fail("Resilience.CircuitBreakerMinimumThroughput deve ser maior ou igual a 2.");

        return ValidateOptionsResult.Success;
    }

    private static bool IsHttpsOrLoopback(Uri uri)
    {
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1");
    }
}
