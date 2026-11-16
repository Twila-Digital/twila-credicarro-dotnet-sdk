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

    private const string WithoutCpfJson = """
        { "cotacoes": [ {
            "valor_solicitado": 31500, "prazo": 24, "valor_parcela": 2033.96, "valor_tac": 1500, "valor_boleto": 0, "valor_seguro": 0,
            "valor_iof": 934.29, "percentual_iof": 3, "valor_credito_total": 34084.29, "valor_divida_total": 48815.04,
            "percentual_cet_mensal": 3.47968, "percentual_cet_anual": 50.7513, "percentual_juros_mensal": 2.8, "percentual_juros_anual": 39.28918,
            "valor_retencao_diaria_aproximado": 12.5, "valor_entrada_minima": 13500 },
          { "valor_solicitado": 31500, "prazo": 36, "valor_parcela": 1500.1, "valor_tac": 1500, "valor_boleto": 0, "valor_seguro": 0,
            "valor_iof": 1000, "percentual_iof": 3, "valor_credito_total": 34000, "valor_divida_total": 54000,
            "percentual_cet_mensal": 3.1, "percentual_cet_anual": 44.2, "percentual_juros_mensal": 2.8, "percentual_juros_anual": 39.3,
            "valor_entrada_minima": 13500 } ] }
        """;

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_ComEntrada_EnviaOCorpoEMapeiaAsOpcoes()
    {
        var inner = RespondWith(WithoutCpfJson);

        var options = await Simulations(inner).CreateFinancingWithoutCpfAsync(
            new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP", 13500m));

        Assert.Equal(2, options.Count);
        var first = options[0];
        Assert.Equal(31500m, first.RequestedAmount);
        Assert.Equal(24, first.Term);
        Assert.Equal(2033.96m, first.InstallmentAmount);
        Assert.Equal(1500m, first.TacAmount);
        Assert.Equal(0m, first.BoletoAmount);
        Assert.Equal(0m, first.InsuranceAmount);
        Assert.Equal(934.29m, first.IofAmount);
        Assert.Equal(3m, first.IofRate);
        Assert.Equal(34084.29m, first.TotalCreditAmount);
        Assert.Equal(48815.04m, first.TotalDebtAmount);
        Assert.Equal(3.47968m, first.MonthlyCet);
        Assert.Equal(50.7513m, first.AnnualCet);
        Assert.Equal(2.8m, first.MonthlyInterestRate);
        Assert.Equal(39.28918m, first.AnnualInterestRate);
        Assert.Equal(12.5m, first.ApproximateDailyRetention);
        Assert.Equal(13500m, first.MinimumDownPayment);

        var request = inner.Requests.Single();
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("/integration/v1/proposal/simulation/financing/without-cpf", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("ABC1D23", body.RootElement.GetProperty("placa").GetString());
        Assert.Equal(45000m, body.RootElement.GetProperty("valorVeiculo").GetDecimal());
        Assert.Equal(13500m, body.RootElement.GetProperty("valorEntrada").GetDecimal());
        Assert.Equal("SP", body.RootElement.GetProperty("uf").GetString());
        Assert.False(body.RootElement.TryGetProperty("cpf", out _));
        Assert.False(body.RootElement.TryGetProperty("cnpjLoja", out _));
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_SemEntrada_NaoEnviaValorEntrada()
    {
        var inner = RespondWith(WithoutCpfJson);

        await Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP"));

        using var body = JsonDocument.Parse(inner.Requests.Single().Body);
        Assert.False(body.RootElement.TryGetProperty("valorEntrada", out _));
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_ComEntradaZero_EnviaZero()
    {
        var inner = RespondWith(WithoutCpfJson);

        await Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP", 0m));

        using var body = JsonDocument.Parse(inner.Requests.Single().Body);
        Assert.Equal(0m, body.RootElement.GetProperty("valorEntrada").GetDecimal());
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_SemRetencaoDiaria_DeixaOCampoNulo()
    {
        var inner = RespondWith(WithoutCpfJson);

        var options = await Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP"));

        Assert.Null(options[1].ApproximateDailyRetention);
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_SemCotacoes_DevolveListaVazia()
    {
        var options = await Simulations(RespondWith("{}")).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP"));

        Assert.Empty(options);
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_NaoGeraNemEnviaIdempotencyKey()
    {
        var inner = RespondWith(WithoutCpfJson);

        await Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP"));

        Assert.False(inner.Requests.Single().HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task CreateFinancingWithoutCpfAsync_ComFalhaTransitoria_RepeteSemIdempotencyKey()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(503)
            : TestApiClientFactory.Json(WithoutCpfJson)));

        var options = await Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP"));

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(2, options.Count);
        Assert.All(inner.Requests, request => Assert.False(request.HasHeader("Idempotency-Key")));
    }

    [Theory]
    [InlineData(404, "Fipe.VehiclePlateNotFound")]
    [InlineData(404, "Store.Integration.NotFound")]
    [InlineData(422, "Store.Integration.Intermediation")]
    [InlineData(422, "Proposal.DownPaymentInvalid")]
    [InlineData(422, "Simulation.Terms.Unavailable")]
    public async Task CreateFinancingWithoutCpfAsync_ComErroDaApi_LancaCredicarroApiException(int status, string errorCode)
    {
        var json = $$"""{ "tipo": "{{errorCode}}", "titulo": "Erro", "status": {{status}} }""";
        var inner = RespondWith(json, (HttpStatusCode)status);

        var exception = await Assert.ThrowsAnyAsync<CredicarroApiException>(
            () => Simulations(inner).CreateFinancingWithoutCpfAsync(new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP")));

        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void CreateFinancingWithoutCpfRequest_ToString_MascaraAPlaca()
    {
        var text = new CreateFinancingWithoutCpfRequest("ABC1D23", 45000m, "SP").ToString();

        Assert.DoesNotContain("ABC1D23", text);
    }

    private const string RefinancingWithoutCpfJson = """
        { "cotacoes": [ {
            "valor_solicitado": 20000, "prazo": 24, "valor_parcela": 1180.5, "valor_tac": 1500, "valor_boleto": 0, "valor_seguro": 0,
            "valor_iof": 600.1, "percentual_iof": 3, "valor_credito_total": 22100.1, "valor_divida_total": 28332,
            "percentual_cet_mensal": 3.2, "percentual_cet_anual": 45.9, "percentual_juros_mensal": 2.8, "percentual_juros_anual": 39.28918,
            "valor_retencao_diaria_aproximado": 9.75, "valor_minimo_refinanciavel": 5000, "valor_maximo_refinanciavel": 30000 },
          { "valor_solicitado": 20000, "prazo": 36, "valor_parcela": 900.2, "valor_tac": 1500, "valor_boleto": 0, "valor_seguro": 0,
            "valor_iof": 700, "percentual_iof": 3, "valor_credito_total": 22200, "valor_divida_total": 32400,
            "percentual_cet_mensal": 3.1, "percentual_cet_anual": 44.2, "percentual_juros_mensal": 2.8, "percentual_juros_anual": 39.3,
            "valor_minimo_refinanciavel": 6000, "valor_maximo_refinanciavel": 40000 } ] }
        """;

    [Fact]
    public async Task CreateRefinancingWithoutCpfAsync_EnviaOCorpoEMapeiaAsOpcoes()
    {
        var inner = RespondWith(RefinancingWithoutCpfJson);

        var options = await Simulations(inner).CreateRefinancingWithoutCpfAsync(
            new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP"));

        Assert.Equal(2, options.Count);
        var first = options[0];
        Assert.Equal(20000m, first.RequestedAmount);
        Assert.Equal(24, first.Term);
        Assert.Equal(1180.5m, first.InstallmentAmount);
        Assert.Equal(1500m, first.TacAmount);
        Assert.Equal(0m, first.BoletoAmount);
        Assert.Equal(0m, first.InsuranceAmount);
        Assert.Equal(600.1m, first.IofAmount);
        Assert.Equal(3m, first.IofRate);
        Assert.Equal(22100.1m, first.TotalCreditAmount);
        Assert.Equal(28332m, first.TotalDebtAmount);
        Assert.Equal(3.2m, first.MonthlyCet);
        Assert.Equal(45.9m, first.AnnualCet);
        Assert.Equal(2.8m, first.MonthlyInterestRate);
        Assert.Equal(39.28918m, first.AnnualInterestRate);
        Assert.Equal(9.75m, first.ApproximateDailyRetention);
        Assert.Equal(5000m, first.MinimumAmount);
        Assert.Equal(30000m, first.MaximumAmount);

        var request = inner.Requests.Single();
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("/integration/v1/proposal/simulation/refinancing/without-cpf", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("ABC1D23", body.RootElement.GetProperty("placa").GetString());
        Assert.Equal(20000m, body.RootElement.GetProperty("valorSolicitado").GetDecimal());
        Assert.Equal("SP", body.RootElement.GetProperty("uf").GetString());
        Assert.False(body.RootElement.TryGetProperty("cpf", out _));
        Assert.False(body.RootElement.TryGetProperty("cnpjLoja", out _));
    }

    [Fact]
    public async Task CreateRefinancingWithoutCpfAsync_SemRetencaoDiaria_DeixaOCampoNulo()
    {
        var inner = RespondWith(RefinancingWithoutCpfJson);

        var options = await Simulations(inner).CreateRefinancingWithoutCpfAsync(new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP"));

        Assert.Null(options[1].ApproximateDailyRetention);
        Assert.Equal(6000m, options[1].MinimumAmount);
        Assert.Equal(40000m, options[1].MaximumAmount);
    }

    [Fact]
    public async Task CreateRefinancingWithoutCpfAsync_SemCotacoes_DevolveListaVazia()
    {
        var options = await Simulations(RespondWith("{}")).CreateRefinancingWithoutCpfAsync(new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP"));

        Assert.Empty(options);
    }

    [Fact]
    public async Task CreateRefinancingWithoutCpfAsync_NaoGeraNemEnviaIdempotencyKey()
    {
        var inner = RespondWith(RefinancingWithoutCpfJson);

        await Simulations(inner).CreateRefinancingWithoutCpfAsync(new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP"));

        Assert.False(inner.Requests.Single().HasHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task CreateRefinancingWithoutCpfAsync_ComFalhaTransitoria_RepeteSemIdempotencyKey()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(503)
            : TestApiClientFactory.Json(RefinancingWithoutCpfJson)));

        var options = await Simulations(inner).CreateRefinancingWithoutCpfAsync(new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP"));

        Assert.Equal(2, inner.CallCount);
        Assert.Equal(2, options.Count);
        Assert.All(inner.Requests, request => Assert.False(request.HasHeader("Idempotency-Key")));
    }

    [Theory]
    [InlineData(404, "Fipe.VehiclePlateNotFound")]
    [InlineData(404, "Store.Integration.NotFound")]
    [InlineData(422, "Proposal.RequestAmountInvalid")]
    [InlineData(422, "Store.Integration.Intermediation")]
    [InlineData(422, "Simulation.Terms.Unavailable")]
    public async Task CreateRefinancingWithoutCpfAsync_ComErroDaApi_LancaCredicarroApiException(int status, string errorCode)
    {
        var json = $$"""{ "tipo": "{{errorCode}}", "titulo": "Erro", "status": {{status}} }""";
        var inner = RespondWith(json, (HttpStatusCode)status);

        var exception = await Assert.ThrowsAnyAsync<CredicarroApiException>(
            () => Simulations(inner).CreateRefinancingWithoutCpfAsync(new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP")));

        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void CreateRefinancingWithoutCpfRequest_ToString_MascaraAPlaca()
    {
        var text = new CreateRefinancingWithoutCpfRequest("ABC1D23", 20000m, "SP").ToString();

        Assert.DoesNotContain("ABC1D23", text);
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
