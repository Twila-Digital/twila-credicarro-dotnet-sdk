using System.Text;
using Credicarro.Errors;
using Credicarro.Proposals.Models;
using Credicarro.Webhooks;
using Credicarro.Webhooks.Models;

namespace Credicarro.UnitTests.Webhooks;

public class CredicarroWebhookEventTests
{
    // Vetor publicado na documentação (webhooks/security.mdx), calculado de forma independente do SDK.
    private const string DocSecret = "segredo-de-exemplo";
    private const string DocTimestamp = "1715792400";
    private const string DocBody = """{"id_proposta":"bea8d060-f93d-48eb-b6c4-45f64483cdd9","enum_status":2,"status":"Approved"}""";
    private const string DocSignature = "db66c554eb116ef2577deb63f72619793502a31520f622b2d59864b074eb8df2";
    private static readonly string DocHeader = $"t={DocTimestamp},v1={DocSignature}";
    private static readonly DateTimeOffset DocTime = DateTimeOffset.FromUnixTimeSeconds(1715792400);

    private const string DetailedBody = """
        {
          "id_proposta": "bea8d060-f93d-48eb-b6c4-45f64483cdd9",
          "enum_status": 2,
          "status": "APROVADA",
          "motivo_evento": "DOCUMENTOS_ALTERADOS",
          "motivo_recusa": null,
          "data_evento": "2026-10-12T17:20:31.512+00:00",
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
              { "enum_tipo": 1, "tipo": "CNH", "enum_status": 2, "status": "INVALIDO", "motivo": "Documento ilegível" }
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
            "documentos": []
          },
          "vendedor": null
        }
        """;

    private static string Sign(string secret, string timestamp, string body) =>
        CredicarroWebhookEvent.ComputeSignature(secret, timestamp, body);

    // ---- Vetor da documentação

    [Fact]
    public void ComputeSignature_ReproduzOVetorDaDocumentacao()
    {
        Assert.Equal(DocSignature, Sign(DocSecret, DocTimestamp, DocBody));
    }

    [Fact]
    public void Parse_ComOVetorDaDocumentacaoDentroDaTolerancia_AceitaEDesserializa()
    {
        var evento = CredicarroWebhookEvent.Parse(DocBody, DocHeader, DocSecret, null, DocTime.AddSeconds(30));

        Assert.Equal(Guid.Parse("bea8d060-f93d-48eb-b6c4-45f64483cdd9"), evento.ProposalId);
        Assert.Equal(ProposalStatus.Approved, evento.Status);
        Assert.Equal(2, evento.StatusRaw);
        Assert.Equal("Approved", evento.StatusName);
        Assert.False(evento.IsDetailed);
        Assert.Null(evento.EventReason);
        Assert.Null(evento.Buyer);
    }

    [Fact]
    public void VerifySignature_ComOVetorDaDocumentacao_RetornaVerdadeiro()
    {
        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, null, DocTime));
    }

    // ---- Janela de tolerância

    [Theory]
    [InlineData(0, true)]
    [InlineData(299, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    [InlineData(-299, true)]
    [InlineData(-300, true)]
    [InlineData(-301, false)]
    [InlineData(3600, false)]
    public void VerifySignature_ValidaATolerancia_NosDoisSentidos(int offsetSeconds, bool expected)
    {
        var now = DocTime.AddSeconds(offsetSeconds);

        Assert.Equal(expected, CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, null, now));
    }

    [Fact]
    public void VerifySignature_ComToleranciaCustomizada_Respeita()
    {
        var now = DocTime.AddMinutes(20);

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, null, now));
        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, TimeSpan.FromMinutes(30), now));
        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, TimeSpan.FromSeconds(10), DocTime.AddSeconds(11)));
    }

    [Fact]
    public void Parse_ComTimestampForaDaJanela_LancaComMensagemSobreReplay()
    {
        var exception = Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(DocBody, DocHeader, DocSecret, null, DocTime.AddMinutes(10)));

        Assert.Contains("tolerância", exception.Message, StringComparison.Ordinal);
    }

    // ---- Assinatura inválida

    [Fact]
    public void Parse_ComCorpoAlterado_Lanca()
    {
        var tampered = DocBody.Replace("\"enum_status\":2", "\"enum_status\":3");

        Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(tampered, DocHeader, DocSecret, null, DocTime));
    }

    [Fact]
    public void Parse_ComCorpoReformatado_Lanca()
    {
        var reformatted = DocBody.Replace(",", ", ");

        Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(reformatted, DocHeader, DocSecret, null, DocTime));
    }

    [Fact]
    public void Parse_ComSegredoErrado_Lanca()
    {
        Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(DocBody, DocHeader, "outro-segredo", null, DocTime));
    }

    [Fact]
    public void Parse_ComAssinaturaAdulterada_Lanca()
    {
        var lastChar = DocSignature[DocSignature.Length - 1];
        var tampered = DocSignature.Substring(0, DocSignature.Length - 1) + (lastChar == '0' ? '1' : '0');

        Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(DocBody, $"t={DocTimestamp},v1={tampered}", DocSecret, null, DocTime));
    }

    [Fact]
    public void Parse_ComTimestampDiferenteDoAssinado_Lanca()
    {
        Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(DocBody, $"t=1715792401,v1={DocSignature}", DocSecret, null, DocTime));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SegredoVazio_ENuncaValido(string? secret)
    {
        // Mesmo uma "assinatura" calculada com chave vazia não pode ser aceita.
        var header = $"t={DocTimestamp},v1={Sign("x", DocTimestamp, DocBody)}";

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, secret!, null, DocTime));
        var exception = Assert.Throws<CredicarroWebhookSignatureException>(
            () => CredicarroWebhookEvent.Parse(DocBody, header, secret!, null, DocTime));
        Assert.Contains("segredo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("header-malformado")]
    [InlineData("t=1715792400")]
    [InlineData("v1=abc")]
    [InlineData("t=,v1=abc")]
    [InlineData("t=1715792400,v1=")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ComCabecalhoMalformado_Lanca(string header)
    {
        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(DocBody, header, DocSecret, null, DocTime));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1715792400")]
    [InlineData("1715792400.5")]
    [InlineData("17157924001")]
    [InlineData("1715 792400")]
    [InlineData("0x1715792")]
    [InlineData("+1715792400")]
    public void VerifySignature_ComTimestampForaDoFormato1a10Digitos_RetornaFalso(string timestamp)
    {
        var header = $"t={timestamp},v1={Sign(DocSecret, timestamp, DocBody)}";

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, TimeSpan.FromDays(100000), DocTime));
    }

    [Fact]
    public void VerifySignature_ComTimestampValidoDeUmDigito_NaoQuebra()
    {
        var header = $"t=5,v1={Sign(DocSecret, "5", DocBody)}";

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, TimeSpan.FromDays(100000), DocTime));
    }

    [Fact]
    public void VerifySignature_AceitaHexEmMaiusculasEEspacosNoCabecalho()
    {
        var header = $"t={DocTimestamp}, v1={DocSignature.ToUpperInvariant()}";

        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
    }

    [Fact]
    public void VerifySignature_ComV1Repetido_EInvalido_MesmoQueUmaDasAssinaturasConfira()
    {
        var header = $"t={DocTimestamp},v1={new string('0', 64)},v1={DocSignature}";

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(DocBody, header, DocSecret, null, DocTime));
    }

    [Fact]
    public void VerifySignature_ComTRepetido_EInvalido()
    {
        var header = $"t={DocTimestamp},t={DocTimestamp},v1={DocSignature}";

        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
    }

    [Fact]
    public void VerifySignature_ComToleranciaNegativa_LancaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, TimeSpan.FromSeconds(-1), DocTime));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CredicarroWebhookEvent.Parse(DocBody, DocHeader, DocSecret, TimeSpan.FromSeconds(-1), DocTime));
        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret, TimeSpan.Zero, DocTime));
    }

    [Theory]
    [InlineData("""{ "enum_status": 2, "status": "APROVADA" }""")]
    [InlineData("""{ "id_proposta": null, "enum_status": 2 }""")]
    [InlineData("""{ "id_proposta": "00000000-0000-0000-0000-000000000000", "enum_status": 2 }""")]
    [InlineData("""{ "id_proposta": "nao-e-uuid", "enum_status": 2 }""")]
    [InlineData("""{ "id_proposta": 123, "enum_status": 2 }""")]
    public void ParseUnverified_SemIdPropostaUuidValido_Lanca(string body)
    {
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.ParseUnverified(body));
    }

    [Fact]
    public void Parse_Verificado_ComAssinaturaValidaMasSemIdProposta_Lanca()
    {
        const string body = """{"enum_status":2,"status":"APROVADA"}""";
        var header = $"t={DocTimestamp},v1={Sign(DocSecret, DocTimestamp, body)}";

        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(body, header, DocSecret, null, DocTime));
    }

    [Fact]
    public void Parse_Verificado_ComHeaderNuloOuSegredoNulo_Lanca()
    {
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(DocBody, null!, DocSecret, null, DocTime));
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(DocBody, DocHeader, null!, null, DocTime));
    }

    [Fact]
    public void ToString_DoEvento_NaoExpoeCpfNemMotivoDaRecusa()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified(DetailedBody.Replace("\"motivo_recusa\": null", "\"motivo_recusa\": \"Restricao do fulano\""));

        var text = evento.ToString();

        Assert.DoesNotContain("12345678909", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Restricao do fulano", text, StringComparison.Ordinal);
        Assert.Contains("***.456.789-**", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPendingActions_DoEventoDetalhado_UsaAMesmaRegraDaConsulta()
    {
        var detalhado = CredicarroWebhookEvent.ParseUnverified(DetailedBody);
        var simples = CredicarroWebhookEvent.ParseUnverified(DocBody);

        var actions = detalhado.GetPendingActions();

        Assert.Equal([PendingActionKind.CorrectDocuments, PendingActionKind.SendDocuments], actions.Select(action => action.Kind).ToArray());
        Assert.Empty(simples.GetPendingActions());
    }

    [Fact]
    public void VerifySignature_ComSegmentosDesconhecidos_Ignora()
    {
        var header = $"v0=lixo,t={DocTimestamp},v1={DocSignature},extra";

        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, header, DocSecret, null, DocTime));
    }

    [Fact]
    public void Parse_UsandoORelogioReal_AceitaAssinaturaRecem_GeradaERecusaAAntiga()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var fresh = $"t={now},v1={Sign(DocSecret, now, DocBody)}";

        var evento = CredicarroWebhookEvent.Parse(DocBody, fresh, DocSecret);

        Assert.Equal(ProposalStatus.Approved, evento.Status);
        Assert.True(CredicarroWebhookEvent.VerifySignature(DocBody, fresh, DocSecret));
        Assert.False(CredicarroWebhookEvent.VerifySignature(DocBody, DocHeader, DocSecret));
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.Parse(DocBody, DocHeader, DocSecret));
    }

    [Fact]
    public void Parse_ComCorpoNulo_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CredicarroWebhookEvent.Parse(null!, DocHeader, DocSecret, null, DocTime));
    }

    // ---- Payloads

    [Fact]
    public void Parse_SemVerificacao_LePayloadSimplesV1EmIngles()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified("""{ "id_proposta": "00000000-0000-0000-0000-000000000001", "enum_status": 1, "status": "Analysing" }""");

        Assert.Equal(ProposalStatus.Analysing, evento.Status);
        Assert.Equal(1, evento.StatusRaw);
        Assert.Equal("Analysing", evento.StatusName);
        Assert.False(evento.IsDetailed);
    }

    [Fact]
    public void Parse_SemVerificacao_LePayloadDetalhadoV2EmPortugues()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified(DetailedBody);

        Assert.True(evento.IsDetailed);
        Assert.Equal(ProposalStatus.Approved, evento.Status);
        Assert.Equal(2, evento.StatusRaw);
        Assert.Equal("APROVADA", evento.StatusName);
        Assert.Equal(ProposalEventReason.DocumentsChanged, evento.EventReason);
        Assert.Equal("DOCUMENTOS_ALTERADOS", evento.EventReasonRaw);
        Assert.Null(evento.RefusalReason);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 17, 20, 31, 512, TimeSpan.Zero), evento.EventDate);
        Assert.Null(evento.Seller);

        var buyer = evento.Buyer!;
        Assert.Equal("12345678909", buyer.Document);
        Assert.Equal(BiometryStatus.Approved, buyer.Biometry);
        Assert.Equal(ProposalStep.DocumentCorrection, buyer.ActiveStep!.Step);
        Assert.Equal(ActionOwner.Integrator, buyer.ActiveStep.ActionOwner);
        Assert.Equal(2, buyer.CompletedSteps.Count);
        Assert.Equal(DocumentStatus.Invalid, buyer.Documents.Single().Status);
        Assert.Equal("Documento ilegível", buyer.Documents.Single().Reason);

        var store = evento.Store!;
        Assert.Equal("11222333000181", store.Document);
        Assert.Equal(ProposalStep.DocumentSubmission, store.ActiveStep!.Step);
    }

    [Fact]
    public void Parse_DoPayloadDetalhado_PermiteUsarOMesmoGetPendingActionsDaConsulta()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified(DetailedBody);

        var details = new ProposalDetails(
            evento.ProposalId, 0, Guid.Empty, evento.Status, evento.StatusRaw, evento.StatusName, ProposalType.Financing, null, null, null,
            evento.Buyer, evento.Seller, evento.Store);

        var actions = details.GetPendingActions();

        Assert.Equal([PendingActionKind.CorrectDocuments, PendingActionKind.SendDocuments], actions.Select(action => action.Kind).ToArray());
    }

    [Theory]
    [InlineData("STATUS_ALTERADO", ProposalEventReason.StatusChanged)]
    [InlineData("ETAPAS_ALTERADAS", ProposalEventReason.StepsChanged)]
    [InlineData("DOCUMENTOS_ALTERADOS", ProposalEventReason.DocumentsChanged)]
    [InlineData("NOVO_MOTIVO", ProposalEventReason.Unknown)]
    public void Parse_MapeiaOMotivoDoEvento(string reason, ProposalEventReason expected)
    {
        var evento = CredicarroWebhookEvent.ParseUnverified($$"""{ "id_proposta": "00000000-0000-0000-0000-000000000001", "enum_status": 2, "status": "APROVADA", "motivo_evento": "{{reason}}" }""");

        Assert.Equal(expected, evento.EventReason);
        Assert.Equal(reason, evento.EventReasonRaw);
        Assert.True(evento.IsDetailed);
    }

    [Theory]
    [InlineData("FINALIZADA", ProposalStatus.Completed, 6)]
    [InlineData("Completed", ProposalStatus.Completed, 6)]
    [InlineData("EM_NEGOCIACAO", ProposalStatus.InNegotiation, 7)]
    [InlineData("PENDENTE", ProposalStatus.Pending, 9)]
    [InlineData("NAO_EXISTE", ProposalStatus.Unknown, -1)]
    public void Parse_SemEnumStatus_UsaONomeEmPortuguesOuIngles(string name, ProposalStatus expected, int expectedRaw)
    {
        var evento = CredicarroWebhookEvent.ParseUnverified($$"""{ "id_proposta": "00000000-0000-0000-0000-000000000001", "status": "{{name}}" }""");

        Assert.Equal(expected, evento.Status);
        Assert.Equal(expectedRaw, evento.StatusRaw);
        Assert.Equal(name, evento.StatusName);
    }

    [Fact]
    public void Parse_ComStatusNumericoDesconhecido_MapeiaParaUnknownPreservandoOBruto()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified("""{ "id_proposta": "00000000-0000-0000-0000-000000000001", "enum_status": 99, "status": "FUTURO" }""");

        Assert.Equal(ProposalStatus.Unknown, evento.Status);
        Assert.Equal(99, evento.StatusRaw);
        Assert.Equal("FUTURO", evento.StatusName);
    }

    [Fact]
    public void Parse_ComMotivoDeRecusa_Expoe()
    {
        var evento = CredicarroWebhookEvent.ParseUnverified("""{ "id_proposta": "00000000-0000-0000-0000-000000000001", "enum_status": 3, "status": "RECUSADA", "motivo_recusa": "Restrição cadastral" }""");

        Assert.Equal(ProposalStatus.Refused, evento.Status);
        Assert.Equal("Restrição cadastral", evento.RefusalReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("isto não é json")]
    [InlineData("null")]
    public void Parse_SemVerificacao_ComCorpoInvalido_Lanca(string body)
    {
        Assert.Throws<CredicarroWebhookSignatureException>(() => CredicarroWebhookEvent.ParseUnverified(body));
    }

    // ---- Header Authorization

    [Theory]
    [InlineData("Basic dXN1YXJpbzpzZW5oYQ==", "Basic dXN1YXJpbzpzZW5oYQ==", true)]
    [InlineData("Bearer abc.def.ghi", "Bearer abc.def.ghi", true)]
    [InlineData("Basic dXN1YXJpbzpzZW5oYQ==", "Basic outro", false)]
    [InlineData("basic dXN1YXJpbzpzZW5oYQ==", "Basic dXN1YXJpbzpzZW5oYQ==", false)]
    [InlineData("", "Basic x", false)]
    [InlineData(null, "Basic x", false)]
    [InlineData("Basic x", null, false)]
    [InlineData("Basic x", "", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public void VerifyAuthorization_ComparaEmTempoConstanteEUsaFalsoParaAusentes(string? received, string? expected, bool result)
    {
        Assert.Equal(result, CredicarroWebhookEvent.VerifyAuthorization(received, expected));
    }

    [Fact]
    public void DefaultTolerance_ECincoMinutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), CredicarroWebhookEvent.DefaultTolerance);
    }
}
