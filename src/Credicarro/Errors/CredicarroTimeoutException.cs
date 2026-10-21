namespace Credicarro.Errors;

/// <summary>
/// Lançada em timeout (por tentativa ou total) ou quando o circuit breaker está aberto.
/// </summary>
public sealed class CredicarroTimeoutException : CredicarroException
{
    public CredicarroTimeoutException(string message) : base(message)
    {
    }

    public CredicarroTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
