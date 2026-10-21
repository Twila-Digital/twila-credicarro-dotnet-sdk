using System.Reflection;
using System.Text.Json.Serialization;
using Credicarro.Http;
using Credicarro.Internal.Generated;
using Credicarro.Internal.Generated.Customer;
using Credicarro.Internal.Generated.Proposals;
using Credicarro.Internal.Generated.Simulations;
using Credicarro.Internal.Generated.Webhooks;
using Credicarro.Internal.Mapping;
using Credicarro.Proposals.Models;
using Xunit;

namespace Credicarro.ContractTests;

/// <summary>
/// Confere os tipos de fio (wire types) do SDK contra o <c>openapi.yaml</c> público: cada requisição ou resposta só usa
/// propriedades que existem no schema da operação, e os campos obrigatórios das requisições existem no wire type.
/// </summary>
[Collection(nameof(OpenApiCollection))]
public sealed class WireContractTests(OpenApiSchemaFixture fixture)
{
    private const string Request = "request";
    private const string Response = "response";

    /// <summary>
    /// (wire type, método, caminho, direção, caminho dentro do schema). Em <c>navigation</c>, um nome desce na
    /// propriedade e <c>[]</c> desce nos itens do array.
    /// </summary>
    public static IEnumerable<object[]> WireTypes()
    {
        // Autenticação
        yield return Case(typeof(Credicarro.Authentication.Models.GenerateAccessTokenRequest), "post", "/v1/authentication/accesstoken", Request);
        yield return Case(typeof(Credicarro.Authentication.Models.GenerateAccessTokenResponse), "post", "/v1/authentication/accesstoken", Response);

        // Cliente
        yield return Case(typeof(CreateCustomerRequestWire), "post", "/v1/customer", Request);
        yield return Case(typeof(CustomerIdResponseWire), "post", "/v1/customer", Response);
        yield return Case(typeof(UpsertCustomerRequestWire), "post", "/v1/customer/unified", Request);
        yield return Case(typeof(CustomerIdResponseWire), "post", "/v1/customer/unified", Response);
        yield return Case(typeof(UpsertAddressRequestWire), "post", "/v1/customer/address", Request);
        yield return Case(typeof(UpsertProfessionalRequestWire), "post", "/v1/customer/professional", Request);

        // Simulação v2
        yield return Case(typeof(FinancingRangeResponseWire), "get", "/v2/proposal/simulation/range/financing", Response);
        yield return Case(typeof(RefinancingRangeResponseWire), "get", "/v2/proposal/simulation/range/refinancing", Response);
        yield return Case(typeof(CreateFinancingSimulationRequestWire), "post", "/v2/proposal/simulation/financing", Request);
        yield return Case(typeof(SimulationListResponseWire), "post", "/v2/proposal/simulation/financing", Response);
        yield return Case(typeof(CreateRefinancingSimulationRequestWire), "post", "/v2/proposal/simulation/refinancing", Request);
        yield return Case(typeof(SimulationListResponseWire), "post", "/v2/proposal/simulation/refinancing", Response);

        // Simulação v1
        yield return Case(typeof(FinancingRangeResponseWire), "get", "/v1/proposal/simulation/range/financing", Response);
        yield return Case(typeof(RefinancingRangeResponseWire), "get", "/v1/proposal/simulation/range/refinancing", Response);
        yield return Case(typeof(CreateFinancingSimulationV1RequestWire), "post", "/v1/proposal/simulation/financing", Request);
        yield return Case(typeof(SimulationListResponseWire), "post", "/v1/proposal/simulation/financing", Response);
        yield return Case(typeof(CreateRefinancingSimulationV1RequestWire), "post", "/v1/proposal/simulation/refinancing", Request);
        yield return Case(typeof(SimulationListResponseWire), "post", "/v1/proposal/simulation/refinancing", Response);
        yield return Case(typeof(SimulationWire), "get", "/v1/proposal/simulation/{simulacaoId}", Response);
        yield return Case(typeof(PagedResultWire<SimulationWire>), "get", "/v1/proposal/simulation", Response);

        // Proposta
        yield return Case(typeof(CreateProposalRequestWire), "post", "/v2/proposal", Request);
        yield return Case(typeof(ProposalIdResponseWire), "post", "/v2/proposal", Response);
        yield return Case(typeof(CreateProposalRequestWire), "post", "/v1/proposal", Request);
        yield return Case(typeof(ProposalIdResponseWire), "post", "/v1/proposal", Response);
        yield return Case(typeof(ProposalV2ResponseWire), "get", "/v2/proposal/{propostaId}", Response);
        yield return Case(typeof(ProposalResponseWire), "get", "/v1/proposal/{propostaId}", Response);
        yield return Case(typeof(PagedResultWire<ProposalResponseWire>), "get", "/v1/proposal", Response);
        yield return Case(typeof(VehicleDetailRequestWire), "post", "/v2/proposal/{propostaId}/vehicle-detail", Request);
        yield return Case(typeof(DepositAccountRequestWire), "post", "/v2/proposal/{propostaId}/deposit-account", Request);
        yield return Case(typeof(DocumentUploadRequestWire), "post", "/v2/proposal/{propostaId}/document", Request);

        // Webhook
        yield return Case(typeof(CreateWebhookRequestWire), "post", "/v1/webhook", Request);
        yield return Case(typeof(CreateWebhookResponseWire), "post", "/v1/webhook", Response);
        yield return Case(typeof(PagedResultWire<WebhookWire>), "get", "/v1/webhook", Response);
        yield return Case(typeof(UpdateWebhookRequestWire), "put", "/v1/webhook/{webhookId}", Request);
    }

    private static object[] Case(Type wireType, string method, string path, string direction) =>
        [wireType.Name.Replace("`1", string.Empty) + " <- " + method.ToUpperInvariant() + " " + path + " (" + direction + ")", wireType, method, path, direction];

    [Theory]
    [MemberData(nameof(WireTypes))]
    public void WireTypeUsesOnlyPropertiesOfTheSchema(string description, Type wireType, string method, string path, string direction)
    {
        Assert.NotNull(description);
        var document = fixture.Document;

        var schema = direction == Request ? document.RequestSchema(method, path) : document.ResponseSchema(method, path);
        Assert.True(schema is not null, $"{method.ToUpperInvariant()} {path} não declara schema de {direction} em application/json.");

        var errors = new List<string>();
        Compare(document, wireType, schema, direction == Request, wireType.Name, errors);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void EverySdkOperationExistsInTheOpenApiAndTheOpenApiHasNoUncoveredOperation()
    {
        var documented = fixture.Document.Operations().Select(operation => operation.Method + " " + operation.Path).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var covered = SdkOperations.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var uncovered = documented.Except(covered).ToArray();
        var missingFromDocs = covered.Except(documented).ToArray();

        Assert.True(uncovered.Length == 0, "Operações do openapi.yaml sem método no SDK: " + string.Join(", ", uncovered));
        Assert.True(missingFromDocs.Length == 0, "Operações do SDK que não existem mais no openapi.yaml: " + string.Join(", ", missingFromDocs));
    }

    /// <summary>
    /// Cada operação coberta por um método público do SDK (ver a tabela de recursos do README).
    /// </summary>
    private static readonly string[] SdkOperations =
    [
        "post /v1/authentication/accesstoken",
        "post /v1/customer",
        "post /v1/customer/unified",
        "post /v1/customer/address",
        "post /v1/customer/professional",
        "get /v2/proposal/simulation/range/financing",
        "get /v2/proposal/simulation/range/refinancing",
        "post /v2/proposal/simulation/financing",
        "post /v2/proposal/simulation/refinancing",
        "get /v1/proposal/simulation/range/financing",
        "get /v1/proposal/simulation/range/refinancing",
        "post /v1/proposal/simulation/financing",
        "post /v1/proposal/simulation/refinancing",
        "get /v1/proposal/simulation/{simulacaoId}",
        "get /v1/proposal/simulation",
        "post /v2/proposal",
        "get /v2/proposal/{propostaId}",
        "post /v2/proposal/{propostaId}/vehicle-detail",
        "post /v2/proposal/{propostaId}/deposit-account",
        "post /v2/proposal/{propostaId}/document",
        "post /v1/proposal",
        "get /v1/proposal/{propostaId}",
        "get /v1/proposal",
        "post /v1/webhook",
        "get /v1/webhook",
        "put /v1/webhook/{webhookId}",
        "delete /v1/webhook/{webhookId}"
    ];

    [Theory]
    [InlineData("get", "/v2/proposal/simulation/range/financing", new[] { "placa", "cnpjLoja" })]
    [InlineData("get", "/v2/proposal/simulation/range/refinancing", new[] { "placa", "cnpjLoja" })]
    [InlineData("get", "/v1/proposal/simulation/range/financing", new[] { "placa" })]
    [InlineData("get", "/v1/proposal/simulation/range/refinancing", new[] { "placa" })]
    [InlineData("get", "/v1/proposal", new[] { "pagina_inicial", "limite" })]
    [InlineData("get", "/v1/proposal/simulation", new[] { "pagina_inicial", "limite" })]
    [InlineData("get", "/v1/webhook", new[] { "pagina_inicial", "limite" })]
    public void QueryParametersUsedBySdkAreDeclared(string method, string path, string[] expected)
    {
        var declared = fixture.Document.Parameters(method, path).Where(parameter => parameter.In == "query").Select(parameter => parameter.Name).ToArray();

        foreach (var name in expected)
            Assert.True(declared.Contains(name), $"{method.ToUpperInvariant()} {path} não declara o parâmetro de query '{name}'.");

        Assert.Equal(expected.Length, declared.Length);
    }

    [Fact]
    public void AutomaticIdempotencyRoutesMatchTheOperationsThatAcceptTheHeader()
    {
        var document = fixture.Document;
        var accepting = document.Operations()
            .Where(operation => operation.Method == "post")
            .Where(operation => document.Parameters(operation.Method, operation.Path).Any(parameter => parameter.In == "header" && parameter.Name == "Idempotency-Key"))
            .Select(operation => operation.Path)
            .ToArray();

        var automatic = document.Operations()
            .Where(operation => operation.Method == "post")
            .Where(operation => ApiRoutes.RequiresAutomaticIdempotencyKey(operation.Path.Replace("{propostaId}", "bea8d060-f93d-48eb-b6c4-45f64483cdd9").TrimStart('/')))
            .Select(operation => operation.Path)
            .ToArray();

        // O SDK nunca gera chave automática para o unified (a chave muda o comportamento da rota).
        var expected = accepting.Where(path => path != "/v1/customer/unified").OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, automatic.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Contains("/v1/customer/unified", accepting);
        Assert.DoesNotContain("/v1/webhook", accepting);
    }

    [Fact]
    public void EnumNamesOfTheSchemaAreKnownBySdk()
    {
        var document = fixture.Document;
        var v2 = document.ResponseSchema("get", "/v2/proposal/{propostaId}");
        var participant = document.Property(v2, "comprador");
        var active = document.Property(participant, "status_ativo");

        foreach (var status in document.EnumValues(document.Property(v2, "status")))
            Assert.NotEqual(ProposalStatus.Unknown, ProposalMapper.ParseStatus(null, status));

        var v1 = document.ResponseSchema("get", "/v1/proposal/{propostaId}");
        foreach (var status in document.EnumValues(document.Property(v1, "status")))
            Assert.NotEqual(ProposalStatus.Unknown, ProposalMapper.ParseStatus(null, status));

        Assert.Equal(Members<ProposalStep>(), document.EnumValues(document.Property(active, "etapa")).Count);
        Assert.Equal(Members<StepSituation>(), document.EnumValues(document.Property(active, "situacao")).Count);
        Assert.Equal(Members<ActionOwner>(), document.EnumValues(document.Property(active, "responsavel_acao")).Count);
        Assert.Equal(Members<BiometryStatus>(), document.EnumValues(document.Property(participant, "biometria")).Count);

        var documents = document.Items(document.Property(participant, "documentos"));
        Assert.Equal(Members<DocumentStatus>(), document.EnumValues(document.Property(documents, "status")).Count);

        var upload = document.RequestSchema("post", "/v2/proposal/{propostaId}/document");
        Assert.Equal(
            new[] { "COMPRADOR", "LOJA", "VENDEDOR" },
            document.EnumValues(document.Property(upload, "participante")).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        var deposit = document.RequestSchema("post", "/v2/proposal/{propostaId}/deposit-account");
        Assert.Equal(
            new[] { "CORRENTE", "PAGAMENTO", "POUPANCA" },
            document.EnumValues(document.Property(deposit, "tipoConta")).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static int Members<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Count(value => Convert.ToInt32(value) != -1);

    private static void Compare(
        OpenApiDocument document,
        Type wireType,
        object? schema,
        bool isRequest,
        string location,
        List<string> errors)
    {
        if (schema is null)
        {
            errors.Add($"{location}: schema ausente no openapi.yaml.");
            return;
        }

        var (schemaProperties, required) = document.ObjectShape(schema);
        var wireProperties = JsonProperties(wireType);

        foreach (var name in wireProperties.Keys.Except(schemaProperties.Keys))
            errors.Add($"{location}: '{name}' não existe no schema.");

        if (isRequest)
        {
            foreach (var name in required.Except(wireProperties.Keys))
                errors.Add($"{location}: o campo obrigatório '{name}' do schema não existe no wire type.");
        }

        foreach (var (name, property) in wireProperties)
        {
            if (!schemaProperties.TryGetValue(name, out var propertySchema))
                continue;

            var nested = NestedWireType(property.PropertyType, out var isArray);

            if (nested is null)
                continue;

            var target = isArray ? document.Items(propertySchema) : document.Resolve(propertySchema);
            Compare(document, nested, target, isRequest, $"{location}.{name}", errors);
        }
    }

    private static Dictionary<string, PropertyInfo> JsonProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name, property => property, StringComparer.Ordinal);

    /// <summary>
    /// Devolve o wire type aninhado da propriedade (diretamente ou como item de lista), ou <see langword="null"/> para escalares.
    /// </summary>
    private static Type? NestedWireType(Type propertyType, out bool isArray)
    {
        isArray = false;
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (type.IsGenericType && type.GetGenericArguments().Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            isArray = true;
            type = type.GetGenericArguments()[0];
        }

        return type.Namespace is not null && type.Namespace.StartsWith("Credicarro.", StringComparison.Ordinal) && type.IsClass ? type : null;
    }
}
