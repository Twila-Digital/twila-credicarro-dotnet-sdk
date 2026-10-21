using YamlDotNet.Serialization;

namespace Credicarro.ContractTests;

/// <summary>
/// Leitura mínima do <c>openapi.yaml</c> (mapas, listas e <c>$ref</c> locais), o bastante para comparar schemas.
/// </summary>
public sealed class OpenApiDocument
{
    private readonly object _root;

    private OpenApiDocument(object root)
    {
        _root = root;
    }

    public static OpenApiDocument Parse(string yaml)
    {
        var root = new DeserializerBuilder().Build().Deserialize<object>(new StringReader(yaml))
            ?? throw new InvalidOperationException("O openapi.yaml está vazio.");

        return new OpenApiDocument(root);
    }

    /// <summary>Pares (método em minúsculas, caminho) de todas as operações do documento.</summary>
    public IReadOnlyList<(string Method, string Path)> Operations()
    {
        var operations = new List<(string, string)>();
        var paths = Map(Get(_root, "paths")) ?? throw new InvalidOperationException("O openapi.yaml não tem 'paths'.");

        foreach (var (path, item) in paths)
        {
            foreach (var (key, _) in Map(item) ?? [])
            {
                if (key is "get" or "post" or "put" or "delete" or "patch")
                    operations.Add((key, path));
            }
        }

        return operations;
    }

    public object? Operation(string method, string path) =>
        Get(Get(Get(_root, "paths"), path), method);

    /// <summary>Schema JSON do corpo da requisição, já resolvido.</summary>
    public object? RequestSchema(string method, string path) =>
        Resolve(Get(Get(Get(Get(Operation(method, path), "requestBody"), "content"), "application/json"), "schema"));

    /// <summary>Schema JSON da primeira resposta 2xx com corpo <c>application/json</c>, já resolvido.</summary>
    public object? ResponseSchema(string method, string path)
    {
        var responses = Map(Get(Operation(method, path), "responses"));

        if (responses is null)
            return null;

        foreach (var (code, response) in responses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!code.StartsWith("2", StringComparison.Ordinal))
                continue;

            var schema = Get(Get(Get(Resolve(response), "content"), "application/json"), "schema");

            if (schema is not null)
                return Resolve(schema);
        }

        return null;
    }

    /// <summary>Parâmetros da operação (nome e local), com <c>$ref</c> resolvido.</summary>
    public IReadOnlyList<(string Name, string In)> Parameters(string method, string path)
    {
        var list = new List<(string, string)>();

        if (Get(Operation(method, path), "parameters") is not List<object> parameters)
            return list;

        foreach (var parameter in parameters)
        {
            var resolved = Resolve(parameter);
            list.Add((Text(Get(resolved, "name")) ?? string.Empty, Text(Get(resolved, "in")) ?? string.Empty));
        }

        return list;
    }

    /// <summary>Propriedades do schema (com <c>allOf</c> mesclado) e os campos obrigatórios.</summary>
    public (IReadOnlyDictionary<string, object?> Properties, IReadOnlyCollection<string> Required) ObjectShape(object? schema)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);
        Collect(Resolve(schema), properties, required);
        return (properties, required);
    }

    public object? Items(object? schema)
    {
        var resolved = Resolve(schema);

        if (Get(resolved, "items") is { } items)
            return Resolve(items);

        if (Get(resolved, "allOf") is List<object> parts)
        {
            foreach (var part in parts)
            {
                if (Items(part) is { } nested)
                    return nested;
            }
        }

        return null;
    }

    public object? Property(object? schema, string name) =>
        ObjectShape(schema).Properties.TryGetValue(name, out var property) ? Resolve(property) : null;

    public IReadOnlyList<string> EnumValues(object? schema)
    {
        var resolved = Resolve(schema);
        return Get(resolved, "enum") is List<object> values ? values.Select(value => Text(value) ?? string.Empty).ToList() : [];
    }

    private void Collect(object? schema, Dictionary<string, object?> properties, HashSet<string> required)
    {
        var resolved = Resolve(schema);

        if (Map(Get(resolved, "properties")) is { } own)
        {
            foreach (var (name, value) in own)
                properties[name] = value;
        }

        if (Get(resolved, "required") is List<object> requiredList)
        {
            foreach (var name in requiredList)
                required.Add(Text(name) ?? string.Empty);
        }

        if (Get(resolved, "allOf") is List<object> parts)
        {
            foreach (var part in parts)
                Collect(part, properties, required);
        }
    }

    /// <summary>Segue <c>$ref</c> locais (<c>#/components/...</c>) até o objeto final.</summary>
    public object? Resolve(object? node)
    {
        for (var depth = 0; depth < 20; depth++)
        {
            var reference = Text(Get(node, "$ref"));

            if (reference is null)
                return node;

            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                throw new InvalidOperationException($"Só $ref locais são suportados: {reference}");

            node = _root;
            foreach (var segment in reference.Substring(2).Split('/'))
                node = Get(node, segment);
        }

        throw new InvalidOperationException("$ref circular ou profundo demais.");
    }

    private static IEnumerable<KeyValuePair<string, object>>? Map(object? node) =>
        node is IDictionary<object, object> dictionary
            ? dictionary.Select(pair => new KeyValuePair<string, object>(Convert.ToString(pair.Key) ?? string.Empty, pair.Value))
            : null;

    private static object? Get(object? node, string key)
    {
        if (node is not IDictionary<object, object> dictionary)
            return null;

        foreach (var pair in dictionary)
        {
            if (string.Equals(Convert.ToString(pair.Key), key, StringComparison.Ordinal))
                return pair.Value;
        }

        return null;
    }

    private static string? Text(object? node) => node as string;
}
