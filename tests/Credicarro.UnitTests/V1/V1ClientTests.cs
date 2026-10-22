using System.Net;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Proposals.Models;
using Credicarro.UnitTests.TestUtilities;
using Credicarro.V1;
using Credicarro.V1.Models;

namespace Credicarro.UnitTests.V1;

public class V1ClientTests
{
    private static readonly Guid ProposalId = Guid.Parse("bea8d060-f93d-48eb-b6c4-45f64483cdd9");

    private const string OptionsJson = """
        { "simulacoes": [ { "id_simulacao": "123e4567-e89b-12d3-a456-426614174000", "valor_solicitado": 1, "prazo": 12, "valor_parcela": 100.5 } ] }
        """;

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    private static IV1SimulationsClient Simulations(FakeHttpMessageHandler inner) =>
        new V1SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static IV1ProposalsClient Proposals(FakeHttpMessageHandler inner) =>
        new V1ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner));

    [Fact]
    public async Task GetFinancingRangeAsync_UsaARotaV1SoComAPlaca()
    {
        var inner = RespondWith("""{ "opcoes_por_prazo": [ { "prazo": 12, "valor_minimo_entrada": 5000 } ] }""");

        var options = await Simulations(inner).GetFinancingRangeAsync("ABC-1234");

        Assert.Equal(12, options.Single().Term);
        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/proposal/simulation/range/financing", request.Path);
        Assert.Equal("?placa=ABC-1234", request.Uri.Query);
    }

    [Fact]
    public async Task GetRefinancingRangeAsync_UsaARotaV1SoComAPlaca()
    {
        var inner = RespondWith("""{ "opcoes_por_prazo": [ { "prazo": 6, "valor_minimo_refinanciavel": 12480, "valor_maximo_refinanciavel": 35720 } ] }""");

        var options = await Simulations(inner).GetRefinancingRangeAsync("ABC1D23");

        Assert.Equal(35720m, options.Single().MaximumAmount);
        Assert.Equal("/integration/v1/proposal/simulation/range/refinancing", inner.Requests.Single().Path);
        Assert.Equal("?placa=ABC1D23", inner.Requests.Single().Uri.Query);
    }

    [Fact]
    public async Task GetRange_SemPlaca_LancaArgumentException()
    {
        var client = Simulations(RespondWith("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetFinancingRangeAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetRefinancingRangeAsync(" "));
    }

    [Fact]
    public async Task CreateFinancingAsync_NaoEnviaCnpjLojaEUsaChaveAutomatica()
    {
        var inner = RespondWith(OptionsJson, HttpStatusCode.Created);

        var options = await Simulations(inner).CreateFinancingAsync(
            new CreateFinancingSimulationV1Request("12345678909", "ABC1D23", 45000m, 5000m, "SP", true));

        Assert.Equal(100.5m, options.Single().InstallmentAmount);

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/proposal/simulation/financing", request.Path);
        Assert.True(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.False(body.RootElement.TryGetProperty("cnpjLoja", out _));
        Assert.True(body.RootElement.GetProperty("isVeiculoZeroKm").GetBoolean());
        Assert.Equal(45000m, body.RootElement.GetProperty("valorVeiculo").GetDecimal());
    }

    [Fact]
    public async Task CreateRefinancingAsync_NaoEnviaCnpjLoja()
    {
        var inner = RespondWith(OptionsJson, HttpStatusCode.Created);

        await Simulations(inner).CreateRefinancingAsync(new CreateRefinancingSimulationV1Request("12345678909", "ABC1D23", 25000m));

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/proposal/simulation/refinancing", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        Assert.False(body.RootElement.TryGetProperty("cnpjLoja", out _));
        Assert.Equal(25000m, body.RootElement.GetProperty("valorSolicitado").GetDecimal());
    }

    [Fact]
    public async Task CreateAsync_UsaARotaV1DeProposta()
    {
        var inner = RespondWith($$"""{ "id_proposta": "{{ProposalId}}" }""", HttpStatusCode.Created);

        var id = await Proposals(inner).CreateAsync(new CreateProposalRequest("12345678909", Guid.Parse("123e4567-e89b-12d3-a456-426614174000")));

        Assert.Equal(ProposalId, id);
        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/proposal", request.Path);
        Assert.True(request.HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task CreateAsync_ComFalhaTransitoria_RepeteComAMesmaChave()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(504)
            : TestApiClientFactory.Json($$"""{ "id_proposta": "{{ProposalId}}" }""", HttpStatusCode.Created)));

        await Proposals(inner).CreateAsync(new CreateProposalRequest("12345678909", Guid.NewGuid()));

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task GetAsync_DevolveAPropostaResumida()
    {
        const string json = """
            {
                "id_proposta": "bea8d060-f93d-48eb-b6c4-45f64483cdd9",
                "numero": 171020250001,
                "id_simulacao": "123e4567-e89b-12d3-a456-426614174000",
                "tipo": "Financing",
                "status": "Approved",
                "comprador": { "cpf": "123.456.789-01" },
                "data_criacao": "2025-10-17T19:05:08.984Z"
            }
            """;
        var inner = RespondWith(json);

        var proposal = await Proposals(inner).GetAsync(ProposalId);

        Assert.Equal("/integration/v1/proposal/bea8d060-f93d-48eb-b6c4-45f64483cdd9", inner.Requests.Single().Path);
        Assert.Equal(ProposalStatus.Approved, proposal.Status);
        Assert.Equal(ProposalType.Financing, proposal.Type);
        Assert.Equal("123.456.789-01", proposal.BuyerCpf);
        Assert.Null(proposal.SellerCpf);
    }

    [Fact]
    public async Task GetAsync_ComPropostaInexistente_Lanca404()
    {
        const string json = """{ "tipo": "Proposal.Access.NotFound", "titulo": "Não encontrado", "status": 404 }""";
        var inner = RespondWith(json, HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(() => Proposals(inner).GetAsync(ProposalId));

        Assert.Equal("Proposal.Access.NotFound", exception.ErrorCode);
    }

    [Fact]
    public void V1Client_ExpoSimulationsEProposals()
    {
        var simulations = Simulations(RespondWith("{}"));
        var proposals = Proposals(RespondWith("{}"));

        IV1Client client = new V1Client(simulations, proposals);

        Assert.Same(simulations, client.Simulations);
        Assert.Same(proposals, client.Proposals);
    }
}
