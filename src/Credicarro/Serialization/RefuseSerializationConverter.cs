using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credicarro.Serialization;

/// <summary>
/// Recusa serializar e desserializar tipos que guardam segredo (opções do client). Lança exceção em vez de devolver um
/// valor mascarado ou perder dados em silêncio.
/// </summary>
internal sealed class RefuseSerializationConverter<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"{typeof(T).Name} guarda segredos e não pode ser desserializado.");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException($"{typeof(T).Name} guarda segredos e não pode ser serializado.");
}
