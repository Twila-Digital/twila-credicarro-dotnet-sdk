namespace Credicarro.Errors;

/// <summary>
/// Exceção base para todos os erros lançados pelo SDK do Credicarro.
/// </summary>
public abstract class CredicarroException : Exception
{
    protected CredicarroException(string message) : base(message)
    {
    }

    protected CredicarroException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
