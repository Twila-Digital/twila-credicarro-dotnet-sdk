using System.Text.Json.Serialization;

namespace Credicarro.Internal.Generated.Proposals;

internal sealed record CreateProposalRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("simulacaoId")] Guid SimulacaoId);

internal sealed record ProposalIdResponseWire(
    [property: JsonPropertyName("id_proposta")] Guid IdProposta);

internal sealed record VehicleDetailRequestWire(
    [property: JsonPropertyName("renavam")] string Renavam,
    [property: JsonPropertyName("chassi")] string Chassi,
    [property: JsonPropertyName("placa")] string Placa,
    [property: JsonPropertyName("cor")] string Cor);

internal sealed record DepositAccountRequestWire(
    [property: JsonPropertyName("cpfTitular")] string CpfTitular,
    [property: JsonPropertyName("codigoBanco")] string CodigoBanco,
    [property: JsonPropertyName("agencia")] string Agencia,
    [property: JsonPropertyName("digitoAgencia")] string DigitoAgencia,
    [property: JsonPropertyName("conta")] string Conta,
    [property: JsonPropertyName("digitoConta")] string DigitoConta,
    [property: JsonPropertyName("tipoConta")] string TipoConta);

internal sealed record DocumentUploadRequestWire(
    [property: JsonPropertyName("participante")] string Participante,
    [property: JsonPropertyName("tipoDocumento")] int TipoDocumento,
    [property: JsonPropertyName("nomeArquivo")] string NomeArquivo,
    [property: JsonPropertyName("conteudoBase64")] byte[] ConteudoBase64);

internal sealed record ProposalResponseWire(
    [property: JsonPropertyName("id_proposta")] Guid IdProposta,
    [property: JsonPropertyName("numero")] long Numero,
    [property: JsonPropertyName("id_simulacao")] Guid IdSimulacao,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("comprador")] ProposalCpfWire? Comprador,
    [property: JsonPropertyName("vendedor")] ProposalCpfWire? Vendedor,
    [property: JsonPropertyName("data_criacao")] DateTimeOffset? DataCriacao);

internal sealed record ProposalCpfWire(
    [property: JsonPropertyName("cpf")] string? Cpf);

internal sealed record ProposalV2ResponseWire(
    [property: JsonPropertyName("id_proposta")] Guid IdProposta,
    [property: JsonPropertyName("numero")] long Numero,
    [property: JsonPropertyName("id_simulacao")] Guid IdSimulacao,
    [property: JsonPropertyName("enum_status")] int? EnumStatus,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("data_criacao")] DateTimeOffset? DataCriacao,
    [property: JsonPropertyName("motivo_recusa")] string? MotivoRecusa,
    [property: JsonPropertyName("link_biometria")] string? LinkBiometria,
    [property: JsonPropertyName("comprador")] ProposalParticipantWire? Comprador,
    [property: JsonPropertyName("vendedor")] ProposalParticipantWire? Vendedor,
    [property: JsonPropertyName("loja")] ProposalStoreWire? Loja);

internal sealed record ProposalParticipantWire(
    [property: JsonPropertyName("cpf")] string? Cpf,
    [property: JsonPropertyName("enum_biometria")] int? EnumBiometria,
    [property: JsonPropertyName("biometria")] string? Biometria,
    [property: JsonPropertyName("status_ativo")] ActiveStepWire? StatusAtivo,
    [property: JsonPropertyName("etapas_concluidas")] IReadOnlyList<CompletedStepWire>? EtapasConcluidas,
    [property: JsonPropertyName("documentos")] IReadOnlyList<ProposalDocumentWire>? Documentos);

internal sealed record ProposalStoreWire(
    [property: JsonPropertyName("cnpj")] string? Cnpj,
    [property: JsonPropertyName("status_ativo")] ActiveStepWire? StatusAtivo,
    [property: JsonPropertyName("etapas_concluidas")] IReadOnlyList<CompletedStepWire>? EtapasConcluidas,
    [property: JsonPropertyName("documentos")] IReadOnlyList<ProposalDocumentWire>? Documentos);

internal sealed record ActiveStepWire(
    [property: JsonPropertyName("enum_etapa")] int EnumEtapa,
    [property: JsonPropertyName("etapa")] string? Etapa,
    [property: JsonPropertyName("enum_situacao")] int EnumSituacao,
    [property: JsonPropertyName("situacao")] string? Situacao,
    [property: JsonPropertyName("enum_responsavel_acao")] int EnumResponsavelAcao,
    [property: JsonPropertyName("responsavel_acao")] string? ResponsavelAcao);

internal sealed record CompletedStepWire(
    [property: JsonPropertyName("enum_etapa")] int EnumEtapa,
    [property: JsonPropertyName("etapa")] string? Etapa,
    [property: JsonPropertyName("data")] DateTimeOffset? Data);

internal sealed record ProposalDocumentWire(
    [property: JsonPropertyName("enum_tipo")] int EnumTipo,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("enum_status")] int EnumStatus,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("motivo")] string? Motivo);
