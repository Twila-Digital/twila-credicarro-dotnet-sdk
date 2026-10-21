using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credicarro.Serialization;

/// <summary>
/// <see cref="JsonSerializerOptions"/> central para (de)serializar requisições e respostas.
/// Campos nulos não são enviados e campos desconhecidos na resposta são ignorados.
/// </summary>
public static class CredicarroJsonOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new UnknownEnumJsonConverterFactory());

        return options;
    }
}
