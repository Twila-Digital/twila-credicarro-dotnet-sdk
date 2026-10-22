namespace Credicarro.Errors;

/// <summary>
/// Lançada quando <c>CredicarroOptions</c> é inválida. A mensagem identifica o campo, nunca o valor.
/// </summary>
public sealed class CredicarroConfigurationException : CredicarroException
{
    public CredicarroConfigurationException(string message) : base(message)
    {
    }

    public CredicarroConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
