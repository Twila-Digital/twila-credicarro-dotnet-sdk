using System.Net;
using System.Text.Json;
using Credicarro.Customers;
using Credicarro.Customers.Models;
using Credicarro.Errors;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Customers;

public class CustomersClientTests
{
    private static readonly Guid CustomerId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

    private static ICustomersClient CreateClient(FakeHttpMessageHandler inner) =>
        new CustomersClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    private static UpsertCustomerRequest FullRequest() => new(
        Cpf: "12345678909",
        FirstName: "Maria",
        LastName: "Souza",
        Email: "maria.souza@exemplo.com.br",
        PhoneNumber: "+55 (11) 99876-5432",
        DateOfBirth: new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
        Address: new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000", Number: "1578", Complement: "Bloco B, Apto 1203"),
        ProfessionalData: new ProfessionalData(OccupationType.Employee, 6500.00m));

    [Fact]
    public async Task UpsertAsync_EnviaOCadastroCompletoSemIdempotencyKey_EDevolveOIdDoCliente()
    {
        var inner = RespondWith($$"""{ "id_cliente": "{{CustomerId}}" }""");

        var id = await CreateClient(inner).UpsertAsync(FullRequest());

        Assert.Equal(CustomerId, id);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v1/customer/unified", request.Path);
        Assert.False(request.HasHeader("Idempotency-Key"));
        Assert.Equal("Bearer fake-token", request.Header("Authorization"));

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("12345678909", root.GetProperty("cpf").GetString());
        Assert.Equal("Maria", root.GetProperty("nome").GetString());
        Assert.Equal("Souza", root.GetProperty("sobrenome").GetString());
        Assert.Equal("maria.souza@exemplo.com.br", root.GetProperty("email").GetString());
        Assert.Equal("+55 (11) 99876-5432", root.GetProperty("celular").GetString());
        Assert.Equal("1990-05-20T00:00:00-03:00", root.GetProperty("dataDeNascimento").GetString());

        var address = root.GetProperty("endereco");
        Assert.Equal("Av. Paulista", address.GetProperty("rua").GetString());
        Assert.Equal("1578", address.GetProperty("numero").GetString());
        Assert.Equal("Bloco B, Apto 1203", address.GetProperty("complemento").GetString());
        Assert.Equal("Bela Vista", address.GetProperty("bairro").GetString());
        Assert.Equal("São Paulo", address.GetProperty("cidade").GetString());
        Assert.Equal("SP", address.GetProperty("estado").GetString());
        Assert.Equal("01311000", address.GetProperty("cep").GetString());

        var professional = root.GetProperty("dadosProfissionais");
        Assert.Equal(3, professional.GetProperty("tipoOcupacao").GetInt32());
        Assert.Equal(6500.00m, professional.GetProperty("renda").GetDecimal());
    }

    [Fact]
    public async Task UpsertAsync_ComChaveExplicita_EnviaAChaveERepeteCasoHajaFalhaTransitoria()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(503)
            : TestApiClientFactory.Json($$"""{ "id_cliente": "{{CustomerId}}" }""")));

        var id = await CreateClient(inner).UpsertAsync(FullRequest(), "criar-maria-001");

        Assert.Equal(CustomerId, id);
        Assert.Equal(2, inner.CallCount);
        Assert.All(inner.Requests, request => Assert.Equal("criar-maria-001", request.Header("Idempotency-Key")));
    }

    [Fact]
    public async Task UpsertAsync_SemChaveComFalhaTransitoria_NaoRepete()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(503)));

        await Assert.ThrowsAsync<CredicarroApiException>(() => CreateClient(inner).UpsertAsync(FullRequest()));

        Assert.Equal(1, inner.CallCount);
        Assert.False(inner.Requests[0].HasHeader("Idempotency-Key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chave-com-acento-ç")]
    [InlineData("quebra\nde-linha")]
    public async Task UpsertAsync_ComChaveInvalida_LancaArgumentException(string key)
    {
        var inner = RespondWith("{}");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(inner).UpsertAsync(FullRequest(), key));

        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task UpsertAsync_ComChaveComMaisDe200Caracteres_LancaArgumentException()
    {
        var inner = RespondWith("{}");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(inner).UpsertAsync(FullRequest(), new string('a', 201)));
    }

    [Fact]
    public async Task UpsertAsync_ComCpfJaCadastradoEChave_Lanca409ComOCodigoEstavel()
    {
        const string json = """{ "tipo": "Cpf.CpfAlreadyAdded", "titulo": "Conflito", "status": 409, "detalhe": "CPF já cadastrado." }""";
        var inner = RespondWith(json, HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(() => CreateClient(inner).UpsertAsync(FullRequest(), "k1"));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("Cpf.CpfAlreadyAdded", exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task UpsertAsync_Com400_LancaValidationExceptionComOCampo()
    {
        const string json = """{ "tipo": "Validation.ZipCode.Invalid", "titulo": "Requisição inválida", "status": 400, "campo": "endereco.cep" }""";
        var inner = RespondWith(json, HttpStatusCode.BadRequest);

        var exception = await Assert.ThrowsAsync<CredicarroValidationException>(() => CreateClient(inner).UpsertAsync(FullRequest()));

        Assert.Equal("endereco.cep", exception.Field);
        Assert.Equal("Validation.ZipCode.Invalid", exception.ErrorCode);
    }

    [Fact]
    public async Task UpsertAsync_ComRespostaVazia_LancaApiException()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(200)));

        await Assert.ThrowsAsync<CredicarroApiException>(() => CreateClient(inner).UpsertAsync(FullRequest()));
    }

    [Fact]
    public async Task UpsertAsync_ComRequestNulo_LancaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateClient(RespondWith("{}")).UpsertAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_EnviaSoOsSeisCamposPessoaisComChaveAutomatica()
    {
        var inner = RespondWith($$"""{ "id_cliente": "{{CustomerId}}" }""");
        var request = new CreateCustomerRequest(
            "12345678909",
            "Maria",
            "Souza",
            "maria.souza@exemplo.com.br",
            "+55 (11) 99876-5432",
            new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)));

        var id = await CreateClient(inner).CreateAsync(request);

        Assert.Equal(CustomerId, id);

        var recorded = inner.Requests.Single();
        Assert.Equal("/integration/v1/customer", recorded.Path);
        Assert.True(Guid.TryParse(recorded.Header("Idempotency-Key"), out _));

        using var body = JsonDocument.Parse(recorded.Body);
        var names = body.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(["celular", "cpf", "dataDeNascimento", "email", "nome", "sobrenome"], names);
    }

    [Fact]
    public async Task CreateAsync_ComFalhaTransitoria_RepeteComAMesmaChave()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(502)
            : TestApiClientFactory.Json($$"""{ "id_cliente": "{{CustomerId}}" }""")));
        var request = new CreateCustomerRequest("12345678909", "Maria", "Souza", "m@x.com", "+55 (11) 99876-5432", DateTimeOffset.UtcNow.AddYears(-30));

        await CreateClient(inner).CreateAsync(request);

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task UpsertAddressAsync_EnviaCpfEEnderecoEAceita202SemCorpo()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(202)));
        var address = new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311-000");

        await CreateClient(inner).UpsertAddressAsync("12345678909", address);

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/customer/address", request.Path);
        Assert.True(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("12345678909", body.RootElement.GetProperty("cpf").GetString());
        var endereco = body.RootElement.GetProperty("endereco");
        Assert.Equal("01311-000", endereco.GetProperty("cep").GetString());
        Assert.False(endereco.TryGetProperty("numero", out _));
        Assert.False(endereco.TryGetProperty("complemento", out _));
    }

    [Fact]
    public async Task UpsertAddressAsync_ComClienteInexistente_Lanca404()
    {
        const string json = """{ "tipo": "Customer.Account.NotFound", "titulo": "Não encontrado", "status": 404 }""";
        var inner = RespondWith(json, HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).UpsertAddressAsync("12345678909", new CustomerAddress("r", "b", "c", "SP", "01311000")));

        Assert.Equal("Customer.Account.NotFound", exception.ErrorCode);
    }

    [Fact]
    public async Task UpsertProfessionalDataAsync_EnviaCpfOcupacaoERenda()
    {
        var inner = new FakeHttpMessageHandler((_, _) => Task.FromResult(TestApiClientFactory.Status(202)));

        await CreateClient(inner).UpsertProfessionalDataAsync("12345678909", new ProfessionalData(OccupationType.BusinessOwner, 12000.50m));

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/customer/professional", request.Path);
        Assert.True(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("12345678909", body.RootElement.GetProperty("cpf").GetString());
        Assert.Equal(7, body.RootElement.GetProperty("tipoOcupacao").GetInt32());
        Assert.Equal(12000.50m, body.RootElement.GetProperty("renda").GetDecimal());
    }

    [Fact]
    public async Task UpsertProfessionalDataAsync_ComOcupacaoInvalida_LancaArgumentException()
    {
        var inner = RespondWith("{}");

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateClient(inner).UpsertProfessionalDataAsync("12345678909", new ProfessionalData((OccupationType)5, 100m)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpsertAddressEProfessional_SemCpf_LancamArgumentException(string? cpf)
    {
        var client = CreateClient(RespondWith("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertAddressAsync(cpf!, new CustomerAddress("r", "b", "c", "SP", "01311000")));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertProfessionalDataAsync(cpf!, new ProfessionalData(OccupationType.Retired, 1m)));
    }
}
