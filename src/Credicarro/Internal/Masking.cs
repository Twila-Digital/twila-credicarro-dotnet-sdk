using System.Text;

namespace Credicarro.Internal;

/// <summary>
/// Máscaras usadas nos <c>ToString</c> de requisições e respostas: nenhum dado pessoal ou segredo sai em logs.
/// </summary>
internal static class Masking
{
    public const string Hidden = "***";

    /// <summary>CPF no formato <c>***.456.789-**</c>; qualquer outro valor vira <c>***</c>.</summary>
    public static string Cpf(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return Hidden;

        var digits = new StringBuilder(11);

        foreach (var character in value!)
        {
            if (character is >= '0' and <= '9')
                digits.Append(character);
        }

        return digits.Length == 11
            ? "***." + digits.ToString(3, 3) + "." + digits.ToString(6, 3) + "-**"
            : Hidden;
    }

    /// <summary>Documento (CPF ou CNPJ): CPF mascarado como acima, o resto vira <c>***</c>.</summary>
    public static string Document(string? value) => Cpf(value);
}
