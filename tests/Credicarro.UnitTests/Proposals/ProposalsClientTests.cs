using System.Net;
using System.Text;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Proposals;
using Credicarro.Proposals.Models;
using Credicarro.UnitTests.TestUtilities;

namespace Credicarro.UnitTests.Proposals;

public class ProposalsClientTests
{
    private static readonly Guid ProposalId = Guid.Parse("bea8d060-f93d-48eb-b6c4-45f64483cdd9");

    private static IProposalsClient CreateClient(FakeHttpMessageHandler inner, Action<Credicarro.Configuration.CredicarroOptions>? configure = null)
    {
        var options = TestApiClientFactory.FastOptions();
        configure?.Invoke(options);
        return new ProposalsClient(TestApiClientFactory.CreateApiHttpClient(inner, options));
    }

    private static FakeHttpMessageHandler RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Json(json, status)));

    private static FakeHttpMessageHandler NoContent() =>
        new((_, _) => Task.FromResult(TestApiClientFactory.Status(204)));

    private const string DetailsJson = """
        {
            "id_proposta": "bea8d060-f93d-48eb-b6c4-45f64483cdd9",
            "numero": 171020250001,
            "id_simulacao": "123e4567-e89b-12d3-a456-426614174000",
            "enum_status": 2,
            "status": "APROVADA",
            "tipo": "Refinancing",
            "data_criacao": "2026-10-12T14:05:08.984-03:00",
            "motivo_recusa": null,
            "link_biometria": "https://onboarding.exemplo.com/sessao/abc123",
            "comprador": {
                "cpf": "12345678909",
                "enum_biometria": 3,
                "biometria": "APROVADA",
                "status_ativo": {
                    "enum_etapa": 7, "etapa": "CORRECAO_DOCUMENTOS",
                    "enum_situacao": 4, "situacao": "PENDENTE_CORRECAO",
                    "enum_responsavel_acao": 2, "responsavel_acao": "INTEGRADOR"
                },
                "etapas_concluidas": [
                    { "enum_etapa": 1, "etapa": "PROPOSTA_CRIADA", "data": "2026-10-12T14:05:08-03:00" },
                    { "enum_etapa": 4, "etapa": "ANALISE_BIOMETRIA", "data": "2026-10-12T14:20:00-03:00" }
                ],
                "documentos": [
                    { "enum_tipo": 1, "tipo": "CNH", "enum_status": 2, "status": "INVALIDO", "motivo": "Documento ilegível" },
                    { "enum_tipo": 5, "tipo": "COMPROVANTE_RESIDENCIA", "enum_status": 1, "status": "VALIDO" },
                    { "enum_tipo": 99, "tipo": "OUTRO_TIPO", "enum_status": 77, "status": "NOVO" }
                ]
            },
            "loja": {
                "cnpj": "11222333000181",
                "status_ativo": {
                    "enum_etapa": 6, "etapa": "ENVIO_DOCUMENTOS",
                    "enum_situacao": 1, "situacao": "AGUARDANDO_ENVIO",
                    "enum_responsavel_acao": 2, "responsavel_acao": "INTEGRADOR"
                },
                "etapas_concluidas": [],
                "documentos": [
                    { "enum_tipo": 8, "tipo": "LAUDO_ECV", "enum_status": 5, "status": "PENDENTE" },
                    { "enum_tipo": 6, "tipo": "DOCUMENTO_VEICULO", "enum_status": 5, "status": "PENDENTE" }
                ]
            }
        }
        """;

    // ---- Create

    [Fact]
    public async Task CreateAsync_EnviaCpfESimulacaoIdComChaveAutomaticaEDevolveOIdDaProposta()
    {
        var inner = RespondWith($$"""{ "id_proposta": "{{ProposalId}}" }""", HttpStatusCode.Created);

        var id = await CreateClient(inner).CreateAsync(new CreateProposalRequest("12345678909", Guid.Parse("123e4567-e89b-12d3-a456-426614174000")));

        Assert.Equal(ProposalId, id);

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v2/proposal", request.Path);
        Assert.True(Guid.TryParse(request.Header("Idempotency-Key"), out _));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("12345678909", body.RootElement.GetProperty("cpf").GetString());
        Assert.Equal("123e4567-e89b-12d3-a456-426614174000", body.RootElement.GetProperty("simulacaoId").GetString());
    }

    [Theory]
    [InlineData("Customer.Account.Incomplete")]
    [InlineData("Proposal.SimulationAlreadyActivated")]
    [InlineData("Store.Integration.Intermediation")]
    public async Task CreateAsync_ComRegraDeNegocio422_LancaBusinessRuleException(string code)
    {
        var inner = RespondWith($$"""{ "tipo": "{{code}}", "titulo": "Regra de negócio não atendida", "status": 422 }""", (HttpStatusCode)422);

        var exception = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(
            () => CreateClient(inner).CreateAsync(new CreateProposalRequest("12345678909", Guid.NewGuid())));

        Assert.Equal(code, exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    // ---- Get / GetPendingActions

    [Fact]
    public async Task GetAsync_MapeiaORetratoCompletoDaProposta()
    {
        var inner = RespondWith(DetailsJson);

        var details = await CreateClient(inner).GetAsync(ProposalId);

        Assert.Equal("/integration/v2/proposal/bea8d060-f93d-48eb-b6c4-45f64483cdd9", inner.Requests.Single().Path);
        Assert.Equal(ProposalId, details.Id);
        Assert.Equal(171020250001, details.Number);
        Assert.Equal(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), details.SimulationId);
        Assert.Equal(ProposalStatus.Approved, details.Status);
        Assert.Equal(2, details.StatusRaw);
        Assert.Equal("APROVADA", details.StatusName);
        Assert.Equal(ProposalType.Refinancing, details.Type);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 14, 5, 8, 984, TimeSpan.FromHours(-3)), details.CreatedAt);
        Assert.Null(details.RefusalReason);
        Assert.Equal("https://onboarding.exemplo.com/sessao/abc123", details.BiometryLink);
        Assert.Null(details.Seller);

        var buyer = details.Buyer!;
        Assert.Equal("12345678909", buyer.Document);
        Assert.Equal(BiometryStatus.Approved, buyer.Biometry);
        Assert.Equal(3, buyer.BiometryRaw);
        Assert.Equal("APROVADA", buyer.BiometryName);

        var active = buyer.ActiveStep!;
        Assert.Equal(ProposalStep.DocumentCorrection, active.Step);
        Assert.Equal(7, active.StepRaw);
        Assert.Equal("CORRECAO_DOCUMENTOS", active.StepName);
        Assert.Equal(StepSituation.PendingCorrection, active.Situation);
        Assert.Equal(4, active.SituationRaw);
        Assert.Equal("PENDENTE_CORRECAO", active.SituationName);
        Assert.Equal(ActionOwner.Integrator, active.ActionOwner);
        Assert.Equal(2, active.ActionOwnerRaw);
        Assert.Equal("INTEGRADOR", active.ActionOwnerName);

        Assert.Equal(2, buyer.CompletedSteps.Count);
        Assert.Equal(ProposalStep.ProposalCreated, buyer.CompletedSteps[0].Step);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 14, 5, 8, TimeSpan.FromHours(-3)), buyer.CompletedSteps[0].Date);

        Assert.Equal(3, buyer.Documents.Count);
        Assert.Equal(DocumentType.Cnh, buyer.Documents[0].Type);
        Assert.Equal(DocumentStatus.Invalid, buyer.Documents[0].Status);
        Assert.Equal("Documento ilegível", buyer.Documents[0].Reason);

        var store = details.Store!;
        Assert.Equal("11222333000181", store.Document);
        Assert.Null(store.Biometry);
        Assert.Equal(ProposalStep.DocumentSubmission, store.ActiveStep!.Step);
        Assert.Empty(store.CompletedSteps);
    }

    [Fact]
    public async Task GetAsync_ComValoresNovosDaApi_MapeiaParaUnknownPreservandoOBruto()
    {
        var inner = RespondWith(DetailsJson);

        var details = await CreateClient(inner).GetAsync(ProposalId);

        var unknownDocument = details.Buyer!.Documents[2];
        Assert.Equal(DocumentType.Unknown, unknownDocument.Type);
        Assert.Equal(99, unknownDocument.TypeRaw);
        Assert.Equal("OUTRO_TIPO", unknownDocument.TypeName);
        Assert.Equal(DocumentStatus.Unknown, unknownDocument.Status);
        Assert.Equal(77, unknownDocument.StatusRaw);
    }

    [Fact]
    public async Task GetAsync_ComStatusENomeNovos_MapeiaParaUnknown_SemLancar()
    {
        var inner = RespondWith("""{ "id_proposta": "bea8d060-f93d-48eb-b6c4-45f64483cdd9", "enum_status": 42, "status": "NOVO_STATUS", "tipo": "Leasing" }""");

        var details = await CreateClient(inner).GetAsync(ProposalId);

        Assert.Equal(ProposalStatus.Unknown, details.Status);
        Assert.Equal(42, details.StatusRaw);
        Assert.Equal("NOVO_STATUS", details.StatusName);
        Assert.Equal(ProposalType.Unknown, details.Type);
        Assert.Null(details.Buyer);
        Assert.Null(details.Store);
    }

    [Fact]
    public async Task GetAsync_ComPropostaDeOutraRota_Lanca422()
    {
        const string json = """{ "tipo": "Proposal.Access.NotIntegrationV2", "titulo": "Regra de negócio não atendida", "status": 422 }""";
        var inner = RespondWith(json, (HttpStatusCode)422);

        var exception = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(() => CreateClient(inner).GetAsync(ProposalId));

        Assert.Equal("Proposal.Access.NotIntegrationV2", exception.ErrorCode);
    }

    [Fact]
    public async Task GetPendingActions_ListaSoOQueCabeAoIntegradorPorParticipante()
    {
        var details = await CreateClient(RespondWith(DetailsJson)).GetAsync(ProposalId);

        var actions = details.GetPendingActions();

        Assert.Equal(2, actions.Count);

        var correction = actions[0];
        Assert.Equal(ProposalParticipant.Buyer, correction.Participant);
        Assert.Equal(ProposalStep.DocumentCorrection, correction.Step);
        Assert.Equal(PendingActionKind.CorrectDocuments, correction.Kind);
        Assert.Equal([DocumentType.Cnh], correction.DocumentTypes);

        var submission = actions[1];
        Assert.Equal(ProposalParticipant.Store, submission.Participant);
        Assert.Equal(ProposalStep.DocumentSubmission, submission.Step);
        Assert.Equal(PendingActionKind.SendDocuments, submission.Kind);
        Assert.Equal([DocumentType.EcvReport, DocumentType.VehicleDocument], submission.DocumentTypes);
    }

    [Fact]
    public void GetPendingActions_ParaVeiculoEContaDeRecebimento_NaoTemTiposDeDocumento()
    {
        static ProposalParticipantState State(ProposalStep step, ActionOwner owner) => new(
            "12345678909",
            null,
            null,
            null,
            new ActiveStep(step, (int)step, null, StepSituation.AwaitingSubmission, 1, null, owner, (int)owner, null),
            [],
            []);

        var details = new ProposalDetails(
            ProposalId, 1, Guid.NewGuid(), ProposalStatus.Approved, 2, "APROVADA", ProposalType.Refinancing, null, null, null,
            Buyer: State(ProposalStep.DepositAccount, ActionOwner.Integrator),
            Seller: null,
            Store: State(ProposalStep.VehicleDetails, ActionOwner.Integrator));

        var actions = details.GetPendingActions();

        Assert.Equal(2, actions.Count);
        Assert.Equal(PendingActionKind.SendDepositAccount, actions[0].Kind);
        Assert.Equal(ProposalParticipant.Buyer, actions[0].Participant);
        Assert.Empty(actions[0].DocumentTypes);
        Assert.Equal(PendingActionKind.SendVehicleDetails, actions[1].Kind);
        Assert.Equal(ProposalParticipant.Store, actions[1].Participant);
    }

    [Theory]
    [InlineData(ActionOwner.Customer)]
    [InlineData(ActionOwner.Credicarro)]
    [InlineData(ActionOwner.None)]
    [InlineData(ActionOwner.Unknown)]
    public void GetPendingActions_QuandoOResponsavelNaoEOIntegrador_NaoListaNada(ActionOwner owner)
    {
        var state = new ProposalParticipantState(
            "12345678909",
            null,
            null,
            null,
            new ActiveStep(ProposalStep.DocumentSubmission, 6, null, StepSituation.AwaitingSubmission, 1, null, owner, (int)owner, null),
            [],
            [new ProposalDocument(DocumentType.Cnh, 1, null, DocumentStatus.Pending, 5, null, null)]);
        var details = new ProposalDetails(
            ProposalId, 1, Guid.NewGuid(), ProposalStatus.Approved, 2, "APROVADA", ProposalType.Financing, null, null, null, state, null, null);

        Assert.Empty(details.GetPendingActions());
    }

    [Fact]
    public void GetPendingActions_SemEtapaAtivaOuParticipantes_DevolveListaVazia()
    {
        var details = new ProposalDetails(
            ProposalId, 1, Guid.NewGuid(), ProposalStatus.Refused, 3, "RECUSADA", ProposalType.Financing, null, "Restrição cadastral", null, null, null, null);

        Assert.Empty(details.GetPendingActions());
    }

    [Fact]
    public void GetPendingActions_EtapasQueNaoExigemAcaoDoIntegrador_SaoIgnoradas()
    {
        var state = new ProposalParticipantState(
            "12345678909",
            null,
            null,
            null,
            new ActiveStep(ProposalStep.Signature, 10, null, StepSituation.AwaitingCustomer, 2, null, ActionOwner.Integrator, 2, null),
            [],
            []);
        var details = new ProposalDetails(
            ProposalId, 1, Guid.NewGuid(), ProposalStatus.Approved, 2, "APROVADA", ProposalType.Financing, null, null, null, state, null, null);

        Assert.Empty(details.GetPendingActions());
    }

    // ---- List

    [Fact]
    public async Task ListAsync_MapeiaAPropostaResumidaEAPaginacao()
    {
        const string json = """
            {
                "itens": [
                    {
                        "id_proposta": "bea8d060-f93d-48eb-b6c4-45f64483cdd9",
                        "numero": 171020250001,
                        "id_simulacao": "123e4567-e89b-12d3-a456-426614174000",
                        "tipo": "Financing",
                        "status": "ManualAnalysis",
                        "comprador": { "cpf": "123.456.789-01" },
                        "vendedor": { "cpf": "987.654.321-00" },
                        "data_criacao": "2025-10-17T19:05:08.984Z"
                    },
                    {
                        "id_proposta": "cea8d060-f93d-48eb-b6c4-45f64483cdd9",
                        "numero": 171020250002,
                        "id_simulacao": "223e4567-e89b-12d3-a456-426614174000",
                        "tipo": "Refinancing",
                        "status": "Qualquer",
                        "comprador": { "cpf": "123.456.789-01" }
                    }
                ],
                "pagina": { "tem_proximo": false, "tem_anterior": true, "numero": 3, "tamanho": 2, "total": 6 }
            }
            """;
        var inner = RespondWith(json);

        var page = await CreateClient(inner).ListAsync(new ListRequest(3, 2));

        Assert.Equal("/integration/v1/proposal", inner.Requests.Single().Path);
        Assert.Equal("?pagina_inicial=3&limite=2", inner.Requests.Single().Uri.Query);

        Assert.Equal(2, page.Items.Count);
        var first = page.Items[0];
        Assert.Equal(ProposalType.Financing, first.Type);
        Assert.Equal(ProposalStatus.ManualAnalysis, first.Status);
        Assert.Equal("ManualAnalysis", first.StatusName);
        Assert.Equal("123.456.789-01", first.BuyerCpf);
        Assert.Equal("987.654.321-00", first.SellerCpf);
        Assert.Equal(171020250001, first.Number);

        Assert.Equal(ProposalStatus.Unknown, page.Items[1].Status);
        Assert.Equal("Qualquer", page.Items[1].StatusName);
        Assert.Null(page.Items[1].SellerCpf);

        Assert.False(page.HasNext);
        Assert.True(page.HasPrevious);
        Assert.Equal(3, page.Page);
        Assert.Equal(6, page.TotalCount);
    }

    // ---- Vehicle detail / deposit account

    [Fact]
    public async Task SendVehicleDetailsAsync_EnviaOsQuatroCamposSemIdempotencyKey()
    {
        var inner = NoContent();

        await CreateClient(inner).SendVehicleDetailsAsync(ProposalId, new VehicleDetails("12345678900", "9BWZZZ377VT004251", "ABC1D23", "PRATA"));

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v2/proposal/bea8d060-f93d-48eb-b6c4-45f64483cdd9/vehicle-detail", request.Path);
        Assert.False(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("12345678900", body.RootElement.GetProperty("renavam").GetString());
        Assert.Equal("9BWZZZ377VT004251", body.RootElement.GetProperty("chassi").GetString());
        Assert.Equal("ABC1D23", body.RootElement.GetProperty("placa").GetString());
        Assert.Equal("PRATA", body.RootElement.GetProperty("cor").GetString());
    }

    [Fact]
    public async Task SendVehicleDetailsAsync_ComFalhaTransitoria_RepeteEntaoTemSemanticaDeSubstituicao()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex == 1 ? 503 : 204)));

        await CreateClient(inner).SendVehicleDetailsAsync(ProposalId, new VehicleDetails("1", "2", "3", "4"));

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task SendVehicleDetailsAsync_ForaDaEtapa_LancaBusinessRuleException()
    {
        const string json = """{ "tipo": "Proposal.InvalidStatus", "titulo": "Regra de negócio não atendida", "status": 422 }""";
        var inner = RespondWith(json, (HttpStatusCode)422);

        var exception = await Assert.ThrowsAsync<CredicarroBusinessRuleException>(
            () => CreateClient(inner).SendVehicleDetailsAsync(ProposalId, new VehicleDetails("1", "2", "3", "4")));

        Assert.Equal("Proposal.InvalidStatus", exception.ErrorCode);
    }

    [Theory]
    [InlineData(DepositAccountType.Checking, "CORRENTE")]
    [InlineData(DepositAccountType.Savings, "POUPANCA")]
    [InlineData(DepositAccountType.Payment, "PAGAMENTO")]
    public async Task SendDepositAccountAsync_EnviaOTipoDeContaEmTexto(DepositAccountType type, string expected)
    {
        var inner = NoContent();
        var account = new DepositAccount("12345678909", "001", "1234", "5", "123456", "7", type);

        await CreateClient(inner).SendDepositAccountAsync(ProposalId, account);

        var request = inner.Requests.Single();
        Assert.Equal("/integration/v2/proposal/bea8d060-f93d-48eb-b6c4-45f64483cdd9/deposit-account", request.Path);
        Assert.False(request.HasHeader("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("12345678909", root.GetProperty("cpfTitular").GetString());
        Assert.Equal("001", root.GetProperty("codigoBanco").GetString());
        Assert.Equal("1234", root.GetProperty("agencia").GetString());
        Assert.Equal("5", root.GetProperty("digitoAgencia").GetString());
        Assert.Equal("123456", root.GetProperty("conta").GetString());
        Assert.Equal("7", root.GetProperty("digitoConta").GetString());
        Assert.Equal(expected, root.GetProperty("tipoConta").GetString());
    }

    [Fact]
    public async Task SendDepositAccountAsync_ComTipoInvalido_LancaArgumentException()
    {
        var account = new DepositAccount("1", "001", "1", "1", "1111", "1", (DepositAccountType)9);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(NoContent()).SendDepositAccountAsync(ProposalId, account));
    }

    // ---- Upload de documento

    private static DocumentFile Pdf() => DocumentFile.FromBytes(Encoding.UTF8.GetBytes("%PDF-1.7 conteudo"), "cnh-comprador.pdf");

    [Theory]
    [InlineData(ProposalParticipant.Buyer, "COMPRADOR")]
    [InlineData(ProposalParticipant.Seller, "VENDEDOR")]
    [InlineData(ProposalParticipant.Store, "LOJA")]
    public async Task UploadDocumentAsync_EnviaParticipanteTipoNomeEBase64(ProposalParticipant participant, string expected)
    {
        var inner = NoContent();

        await CreateClient(inner).UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(participant, DocumentType.Cnh, Pdf()), "doc-7f3c2a10");

        var request = inner.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/integration/v2/proposal/bea8d060-f93d-48eb-b6c4-45f64483cdd9/document", request.Path);
        Assert.Equal("doc-7f3c2a10", request.Header("Idempotency-Key"));

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(expected, root.GetProperty("participante").GetString());
        Assert.Equal(1, root.GetProperty("tipoDocumento").GetInt32());
        Assert.Equal("cnh-comprador.pdf", root.GetProperty("nomeArquivo").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-1.7 conteudo")), root.GetProperty("conteudoBase64").GetString());
    }

    [Fact]
    public async Task UploadDocumentAsync_SemChave_GeraUmaChavePorChamada()
    {
        var inner = NoContent();
        var client = CreateClient(inner);
        var upload = new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.ProofOfIncome, Pdf());

        await client.UploadDocumentAsync(ProposalId, upload);
        await client.UploadDocumentAsync(ProposalId, upload);

        var first = inner.Requests[0].Header("Idempotency-Key");
        var second = inner.Requests[1].Header("Idempotency-Key");
        Assert.True(Guid.TryParse(first, out _));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task UploadDocumentAsync_ComFalhaTransitoria_ReusaAMesmaChaveNasTentativas()
    {
        var inner = new FakeHttpMessageHandler((_, callIndex) => Task.FromResult(TestApiClientFactory.Status(callIndex <= 2 ? 502 : 204)));

        await CreateClient(inner).UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()));

        Assert.Equal(3, inner.CallCount);
        Assert.Single(inner.Requests.Select(request => request.Header("Idempotency-Key")).Distinct());
        Assert.Single(inner.Requests.Select(request => request.Body).Distinct());
    }

    [Fact]
    public async Task UploadDocumentAsync_ComDisableAutomaticIdempotencyKey_AindaEnviaAChaveObrigatoria()
    {
        var inner = NoContent();

        await CreateClient(inner, options => options.Resilience.DisableAutomaticIdempotencyKey = true)
            .UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()));

        Assert.True(Guid.TryParse(inner.Requests.Single().Header("Idempotency-Key"), out _));
    }

    [Fact]
    public async Task UploadDocumentAsync_ComChaveReutilizadaComOutroConteudo_Lanca409()
    {
        const string json = """{ "tipo": "Idempotency.KeyConflict", "titulo": "Conflito", "status": 409 }""";
        var inner = RespondWith(json, HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<CredicarroApiException>(
            () => CreateClient(inner).UploadDocumentAsync(ProposalId, new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf()), "k"));

        Assert.Equal("Idempotency.KeyConflict", exception.ErrorCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task UploadDocumentAsync_ComTipoDesconhecido_LancaArgumentException()
    {
        var upload = new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Unknown, Pdf());

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(NoContent()).UploadDocumentAsync(ProposalId, upload));
    }

    [Fact]
    public async Task UploadDocumentAsync_ComChaveInvalida_LancaArgumentException()
    {
        var upload = new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, Pdf());

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(NoContent()).UploadDocumentAsync(ProposalId, upload, ""));
    }
}
