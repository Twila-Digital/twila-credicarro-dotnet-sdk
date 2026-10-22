using System.Text.Json;

namespace Credicarro.Errors;

/// <summary>
/// Um item da lista <c>erros</c> do <c>ProblemDetails</c> (aparece quando o JSON enviado está malformado ou tem um valor
/// de tipo errado).
/// </summary>
public sealed record ProblemErrorModel(string? Code, string? Message, string? Field)
{
    /// <summary>Sem a mensagem nem o campo (podem citar valores informados).</summary>
    public override string ToString() => $"ProblemErrorModel {{ Code = {Code} }}";
}

/// <summary>
/// Modelo do <c>ProblemDetails</c> devolvido pela API em respostas de erro: <c>tipo</c>, <c>titulo</c>, <c>status</c>,
/// <c>detalhe</c>, <c>campo</c> e <c>erros</c>. Também lê o formato padrão (<c>type</c>, <c>title</c>, <c>detail</c>)
/// usado em falhas inesperadas. Campos ausentes, nulos ou de tipo inesperado nunca causam falha de leitura.
/// </summary>
public sealed class ProblemDetailsModel
{
    /// <summary>Código estável do erro (<c>tipo</c>, ou <c>type</c> como alternativa). Use este campo no tratamento.</summary>
    public string? Type { get; set; }

    /// <summary>Categoria do erro (<c>titulo</c>, ou <c>title</c>).</summary>
    public string? Title { get; set; }

    /// <summary>Status HTTP informado no corpo.</summary>
    public int? Status { get; set; }

    /// <summary>Mensagem do erro (<c>detalhe</c>, ou <c>detail</c>). Pode conter dados pessoais; não registre em logs.</summary>
    public string? Detail { get; set; }

    /// <summary>Campo da requisição relacionado ao erro, quando houver (ex.: <c>endereco.cep</c>).</summary>
    public string? Field { get; set; }

    /// <summary>Lista <c>erros</c>, quando a API a retornou.</summary>
    public IReadOnlyList<ProblemErrorModel> Errors { get; set; } = Array.Empty<ProblemErrorModel>();

    internal static ProblemDetailsModel Empty() => new();

    /// <summary>
    /// Lê o corpo de uma resposta de erro. Corpo vazio, que não é JSON ou que não é um objeto resulta em um modelo vazio.
    /// </summary>
    public static ProblemDetailsModel Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Empty();

        try
        {
            using var document = JsonDocument.Parse(body!);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return Empty();

            var model = new ProblemDetailsModel
            {
                Type = ReadString(root, "tipo") ?? ReadString(root, "type"),
                Title = ReadString(root, "titulo") ?? ReadString(root, "title"),
                Detail = ReadString(root, "detalhe") ?? ReadString(root, "detail"),
                Field = ReadString(root, "campo"),
                Status = ReadInt(root, "status"),
            };

            if (TryGetProperty(root, "erros", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var list = new List<ProblemErrorModel>();

                foreach (var item in errors.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    list.Add(new ProblemErrorModel(ReadString(item, "codigo"), ReadString(item, "mensagem"), ReadString(item, "campo")));
                }

                model.Errors = list;
            }

            return model;
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? ReadInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
}
