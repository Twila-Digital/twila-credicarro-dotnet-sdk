using System.Text.Json.Serialization;

namespace Credicarro.Internal.Generated;

internal sealed record PagedResultWire<T>(
    [property: JsonPropertyName("itens")] IReadOnlyList<T>? Items,
    [property: JsonPropertyName("pagina")] PageWire? Page);

internal sealed record PageWire(
    [property: JsonPropertyName("tem_proximo")] bool HasNext,
    [property: JsonPropertyName("tem_anterior")] bool HasPrevious,
    [property: JsonPropertyName("numero")] int Number,
    [property: JsonPropertyName("tamanho")] int Size,
    [property: JsonPropertyName("total")] long Total);
