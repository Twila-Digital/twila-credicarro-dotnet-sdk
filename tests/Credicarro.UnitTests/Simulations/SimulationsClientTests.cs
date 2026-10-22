using System.Net;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Simulations;
using Credicarro.Simulations.Models;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Simulations;

public class SimulationsClientTests
{
    private static readonly Guid SimulationId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

    private static ISimulationsClient CreateClient(FakeHttpMessageHandler inner) =>
        new SimulationsClient(TestApiClientFactory.CreateApiHttpClient(inner));

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    private const string OptionsJson = """
        {
            "simulacoes": [
                {
                    "id_simulacao": "123e4567-e89b-12d3-a456-426614174000",
                    "valor_solicitado": 40000,
                    "prazo": 36,
                    "valor_parcela": 2042.42,
                    "valor_tac": 1500,
                    "valor_boleto": 3.5,
                    "valor_seguro": 0,
                    "valor_iof": 872,
                    "percentual_iof": 1.92,
                    "valor_credito_total": 42722,
                    "valor_divida_total": 73527.12,
                    "percentual_cet_mensal": 3.62,
                    "percentual_cet_anual": 53.25,
                    "percentual_juros_mensal": 3.29,
                    "percentual_juros_anual": 47.53,
                    "valor_retencao_diaria_aproximado": 12.5
                },
                {
                    "id_simulacao": "223e4567-e89b-12d3-a456-426614174000",
                    "valor_solicitado": 40000,
                    "prazo": 48,
                    "valor_parcela": 1800,
                    "valor_tac": 1500,
                    "valor_boleto": 3.5,
                    "valor_seguro": 0,
                    "valor_iof": 900,
                    "percentual_iof": 2.1,
                    "valor_credito_total": 42900,
                    "valor_divida_total": 86400,
                    "percentual_cet_mensal": 3.5,
                    "percentual_cet_anual": 51.1,
                    "percentual_juros_mensal": 3.2,
                    "percentual_juros_anual": 46.2
                }
            ]
        }
        """;

    [Fact]
    public async Task GetFinancingRangeAsync_EnviaPlacaECnpjLojaELeAsOpcoes()
    {
        const string json = """
            { "opcoes_por_prazo": [ { "prazo": 12, "valor_minimo_entrada": 5000.00 }, { "prazo": 24, "valor_minimo_entrada": 4500.5 } ] }
            """;
        var inner = RespondWith(json);

        var options = await CreateClient(inner).GetFinancingRangeAsync("ABC-1234", "11.222.333/0001-81");

        Assert.Equal(2, options.Count);
        Assert.Equal(new FinancingRangeOption(12, 5000.00m), options[0]);
        Assert.Equal(new FinancingRangeOption(24, 4500.5m), options[1]);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/integration/v2/proposal/simulation/range/financing", request.Path);
        Assert.Contains("placa=ABC-1234", request.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("cnpjLoja=11.222.333%2F0001-81", request.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRefinancingRangeAsync_LeMinimoEMaximo()
    {
        const string json = """
            { "opcoes_por_prazo": [ { "prazo": 6, "valor_minimo_refinanciavel": 12480, "valor_maximo_refinanciavel": 35720 } ] }
            """;
        var inner = RespondWith(json);

        var options = await CreateClient(inner).GetRefinancingRangeAsync("ABC1D23", "11222333000181");

        Assert.Equal(new RefinancingRangeOption(6, 12480m, 35720m), Assert.Single(options));
        Assert.Equal("/integration/v2/proposal/simulation/range/refinancing", inner.Requests.Single().Path);
    }

    [Fact]
    public async Task GetFinancingRangeAsync_SemOpcoes_DevolveListaVazia()
    {
        var options = await CreateClient(RespondWith("{}")).GetFinancingRangeAsync("ABC1D23", "11222333000181");

        Assert.Empty(options);
    }

    [Theory]
    [InlineData("", "11222333000181")]
    [InlineData("ABC1D23", "")]
    [InlineData(" ", "11222333000181")]
    public async Task GetRange_SemPlacaOuCnpj_LancaArgumentException(string plate, string cnpj)
    {
        var client = CreateClient(RespondWith("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetFinancingRangeAsync(plate, cnpj));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetRefinancingRangeAsync(plate, cnpj));
    }

    [Fact]
    public async Task GetFinancingRangeAsync_ComLojaDeIntermediacao_Lanca422ComOCodigo()
    {
        const string json = """{ "tipo": "Store.Integration.Intermediation", "titulo": "Regra de negócio não atendida", "status": 422 }""";
        var inner = RespondWith(json, (HttpStatusCode)422);

        var exception = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(
            () => CreateClient(inner).GetFinancingRangeAsync("ABC1D23", "11222333000181"));

        Assert.Equal("Store.Integration.Intermediation", exception.ErrorCode);
    }

    [Fact]
    public async Task CreateFinancingAsync_EnviaOPayloadV2ComCnpjLojaEChaveAutomatica()
    {
        var inner = RespondWith(OptionsJson, HttpStatusCode.Created);
        var request = new CreateFinancingSimulationRequest("12345678909", "ABC1D23", 45000m, 5000m, "SP", false, "11.222.333/0001-81");

        var options = await CreateClient(inner).CreateFinancingAsync(request);

        Assert.Equal(2, options.Count);

        var first = options[0];
        Assert.Equal(SimulationId, first.SimulationId);
        Assert.Equal(40000m, first.RequestedAmount);
        Assert.Equal(36, first.Term);
        Assert.Equal(2042.42m, first.InstallmentAmount);
        Assert.Equal(1500m, first.TacAmount);
        Assert.Equal(3.5m, first.BoletoAmount);
        Assert.Equal(0m, first.InsuranceAmount);
        Assert.Equal(872m, first.IofAmount);
        Assert.Equal(1.92m, first.IofRate);
        Assert.Equal(42722m, first.TotalCreditAmount);
        Assert.Equal(73527.12m, first.TotalDebtAmount);
        Assert.Equal(3.62m, first.MonthlyCet);
        Assert.Equal(53.25m, first.AnnualCet);
        Assert.Equal(3.29m, first.MonthlyInterestRate);
        Assert.Equal(47.53m, first.AnnualInterestRate);
        Assert.Equal(12.5m, first.ApproximateDailyRetention);
        Assert.Null(options[1].ApproximateDailyRetention);

        var recorded = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, recorded.Method);
        Assert.Equal("/integration/v2/proposal/simulation/financing", recorded.Path);
        Assert.True(recorded.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(recorded.Body);
        var root = body.RootElement;
        Assert.Equal("12345678909", root.GetProperty("cpf").GetString());
        Assert.Equal("ABC1D23", root.GetProperty("placa").GetString());
        Assert.Equal(45000m, root.GetProperty("valorVeiculo").GetDecimal());
        Assert.Equal(5000m, root.GetProperty("valorEntrada").GetDecimal());
        Assert.Equal("SP", root.GetProperty("uf").GetString());
        Assert.False(root.GetProperty("isVeiculoZeroKm").GetBoolean());
        Assert.Equal("11.222.333/0001-81", root.GetProperty("cnpjLoja").GetString());
    }

    [Fact]
    public async Task CreateFinancingAsync_ComFalhaTransitoria_RepeteComAMesmaChave()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(callIndex == 1
            ? TestApiClientFactory.Status(503)
            : TestApiClientFactory.Json(OptionsJson, HttpStatusCode.Created)));
        var request = new CreateFinancingSimulationRequest("12345678909", "ABC1D23", 45000m, 0m, "SP", true, "11222333000181");

        var options = await CreateClient(inner).CreateFinancingAsync(request);

        Assert.Equal(2, options.Count);
        Assert.Equal(2, inner.CallCount);
        Assert.Equal(inner.Requests[0].Header("Idempotency-Key"), inner.Requests[1].Header("Idempotency-Key"));
    }

    [Fact]
    public async Task CreateRefinancingAsync_EnviaOPayloadV2()
    {
        var inner = RespondWith(OptionsJson, HttpStatusCode.Created);

        var options = await CreateClient(inner).CreateRefinancingAsync(
            new CreateRefinancingSimulationRequest("12345678909", "ABC1D23", 25000m, "11222333000181"));

        Assert.Equal(2, options.Count);

        var recorded = inner.Requests.Single();
        Assert.Equal("/integration/v2/proposal/simulation/refinancing", recorded.Path);
        Assert.True(recorded.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(recorded.Body);
        Assert.Equal(25000m, body.RootElement.GetProperty("valorSolicitado").GetDecimal());
        Assert.Equal("11222333000181", body.RootElement.GetProperty("cnpjLoja").GetString());
        Assert.False(body.RootElement.TryGetProperty("valorVeiculo", out _));
    }

    [Fact]
    public async Task CreateFinancingAsync_ComIdempotencyKeyReutilizada_Lanca409()
    {
        const string json = """{ "tipo": "Proposal.SimulationIdempotencyConflict", "titulo": "Conflito", "status": 409 }""";
        var inner = RespondWith(json, HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).CreateFinancingAsync(new CreateFinancingSimulationRequest("1", "A", 1m, 0m, "SP", true, "1")));

        Assert.Equal("Proposal.SimulationIdempotencyConflict", exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task GetAsync_MapeiaASimulacaoCompleta()
    {
        const string json = """
            {
                "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9",
                "prazo": 24,
                "valor_parcela": 2356,
                "valor_solicitado": 37908,
                "valor_entrada": 9786,
                "valor_bem": 47694,
                "valor_tac": 1500,
                "taxa_juros_negociada": 2.79,
                "valor_registro": 487,
                "uf": "SP",
                "tipo": "Refinancing",
                "transacao": { "valor_total_credito": 78495, "valor_total_debito": 78496 },
                "juros": { "percentual_juros_mensal": 3.29, "percentual_juros_anual": 39.48 },
                "cet": { "percentual_mensal": 3.62, "percentual_anual": 43.44 },
                "iof": { "valor": 872, "percentual": 1.92 },
                "veiculo": {
                    "marca": "Honda", "modelo": "Civic", "versao": "Turing", "ano": 2024, "valor": 124654,
                    "placa": "MWO-3910", "veiculo_zero_km": false, "chassi": "1Y5Ye4U8kydsr4828", "renavam": "70502561697", "cor": "Prata"
                },
                "cliente": { "cpf": "123.456.789-01" },
                "data_criacao": "2025-10-17T19:05:08.984Z"
            }
            """;
        var inner = RespondWith(json);

        var simulation = await CreateClient(inner).GetAsync(Guid.Parse("bea8d060-f93d-48eb-b6c4-45f64483cdd9"));

        Assert.Equal("/integration/v1/proposal/simulation/bea8d060-f93d-48eb-b6c4-45f64483cdd9", inner.Requests.Single().Path);
        Assert.Equal(24, simulation.Term);
        Assert.Equal(2356m, simulation.InstallmentAmount);
        Assert.Equal(37908m, simulation.RequestedAmount);
        Assert.Equal(9786m, simulation.DownPayment);
        Assert.Equal(47694m, simulation.AssetAmount);
        Assert.Equal(1500m, simulation.TacAmount);
        Assert.Equal(2.79m, simulation.NegotiatedInterestRate);
        Assert.Equal(487m, simulation.RegistrationAmount);
        Assert.Equal("SP", simulation.State);
        Assert.Equal(SimulationType.Refinancing, simulation.Type);
        Assert.Equal(new SimulationTransaction(78495m, 78496m), simulation.Transaction);
        Assert.Equal(new SimulationInterest(3.29m, 39.48m), simulation.Interest);
        Assert.Equal(new SimulationCet(3.62m, 43.44m), simulation.Cet);
        Assert.Equal(new SimulationIof(872m, 1.92m), simulation.Iof);
        Assert.Equal("Honda", simulation.Vehicle!.Brand);
        Assert.Equal(2024, simulation.Vehicle.Year);
        Assert.Equal(124654m, simulation.Vehicle.Value);
        Assert.False(simulation.Vehicle.IsZeroKm);
        Assert.Equal("MWO-3910", simulation.Vehicle.LicensePlate);
        Assert.Equal("123.456.789-01", simulation.CustomerCpf);
        Assert.Equal(new DateTimeOffset(2025, 10, 17, 19, 5, 8, 984, TimeSpan.Zero), simulation.CreatedAt);
    }

    [Fact]
    public async Task GetAsync_ComTipoDesconhecido_MapeiaParaUnknown_SemLancar()
    {
        var inner = RespondWith("""{ "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9", "tipo": "Leasing" }""");

        var simulation = await CreateClient(inner).GetAsync(Guid.NewGuid());

        Assert.Equal(SimulationType.Unknown, simulation.Type);
        Assert.Null(simulation.Vehicle);
        Assert.Null(simulation.Transaction);
    }

    [Fact]
    public async Task GetAsync_ComSimulacaoInexistente_Lanca404()
    {
        const string json = """{ "tipo": "Simulation.Access.NotFound", "titulo": "Não encontrado", "status": 404 }""";
        var inner = RespondWith(json, HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(() => CreateClient(inner).GetAsync(Guid.NewGuid()));

        Assert.Equal("Simulation.Access.NotFound", exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ListAsync_UsaPaginaInicialELimiteEMapeiaAPaginacao()
    {
        const string json = """
            {
                "itens": [ { "id_simulacao": "bea8d060-f93d-48eb-b6c4-45f64483cdd9", "prazo": 12, "tipo": "Financing" } ],
                "pagina": { "tem_proximo": true, "tem_anterior": false, "numero": 2, "tamanho": 15, "total": 45 }
            }
            """;
        var inner = RespondWith(json);

        var page = await CreateClient(inner).ListAsync(new ListRequest(Page: 2, PageSize: 15));

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v1/proposal/simulation", request.Path);
        Assert.Equal("?pagina_inicial=2&limite=15", request.Uri.Query);

        Assert.Single(page.Items);
        Assert.Equal(SimulationType.Financing, page.Items[0].Type);
        Assert.True(page.HasNext);
        Assert.False(page.HasPrevious);
        Assert.Equal(2, page.Page);
        Assert.Equal(15, page.PageSize);
        Assert.Equal(45, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_SemParametros_UsaPaginaUmELimiteDez()
    {
        var inner = RespondWith("""{ "itens": [], "pagina": { "tem_proximo": false, "tem_anterior": false, "numero": 1, "tamanho": 10, "total": 0 } }""");

        var page = await CreateClient(inner).ListAsync();

        Assert.Equal("?pagina_inicial=1&limite=10", inner.Requests.Single().Uri.Query);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListAsync_ComPaginacaoInvalida_LancaArgumentOutOfRangeException(int page, int pageSize)
    {
        var inner = RespondWith("{}");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateClient(inner).ListAsync(new ListRequest(page, pageSize)));

        Assert.Equal(0, inner.CallCount);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    public async Task ListAsync_ComLimitesValidos_Funciona(int page, int pageSize)
    {
        var inner = RespondWith("""{ "itens": [], "pagina": { "numero": 1, "tamanho": 1, "total": 0 } }""");

        await CreateClient(inner).ListAsync(new ListRequest(page, pageSize));

        Assert.Equal(1, inner.CallCount);
    }
}
