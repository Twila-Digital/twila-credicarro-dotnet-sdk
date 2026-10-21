using Credicarro.Internal.Generated.Proposals;
using Credicarro.Proposals.Models;
using Credicarro.Serialization;

namespace Credicarro.Internal.Mapping;

internal static class ProposalMapper
{
    /// <summary>
    /// Nomes de status em português (consulta e webhook detalhado) e em inglês (listagem e webhook simples).
    /// </summary>
    private static readonly Dictionary<string, ProposalStatus> StatusByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EM_ANALISE"] = ProposalStatus.Analysing,
        ["APROVADA"] = ProposalStatus.Approved,
        ["RECUSADA"] = ProposalStatus.Refused,
        ["CANCELADA"] = ProposalStatus.Canceled,
        ["ANALISE_MANUAL"] = ProposalStatus.ManualAnalysis,
        ["FINALIZADA"] = ProposalStatus.Completed,
        ["EM_NEGOCIACAO"] = ProposalStatus.InNegotiation,
        ["EXPIRADA"] = ProposalStatus.Expired,
        ["PENDENTE"] = ProposalStatus.Pending,
        ["Analysing"] = ProposalStatus.Analysing,
        ["Approved"] = ProposalStatus.Approved,
        ["Refused"] = ProposalStatus.Refused,
        ["Canceled"] = ProposalStatus.Canceled,
        ["ManualAnalysis"] = ProposalStatus.ManualAnalysis,
        ["Completed"] = ProposalStatus.Completed,
        ["InNegotiation"] = ProposalStatus.InNegotiation,
        ["Expired"] = ProposalStatus.Expired,
        ["Pending"] = ProposalStatus.Pending
    };

    /// <summary>
    /// O código numérico tem prioridade; sem ele, usa o nome (português ou inglês).
    /// </summary>
    public static ProposalStatus ParseStatus(int? raw, string? name)
    {
        if (raw is { } code)
            return EnumMapping.FromWireValue<ProposalStatus>(code);

        if (!string.IsNullOrWhiteSpace(name) && StatusByName.TryGetValue(name!.Trim(), out var status))
            return status;

        return ProposalStatus.Unknown;
    }

    private static int StatusRaw(int? raw, ProposalStatus status) =>
        raw ?? (status == ProposalStatus.Unknown ? -1 : (int)status);

    public static CreateProposalRequestWire ToWire(CreateProposalRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateProposalRequestWire(request.Cpf, request.SimulationId);
    }

    public static VehicleDetailRequestWire ToWire(VehicleDetails details)
    {
        if (details is null)
            throw new ArgumentNullException(nameof(details));

        return new VehicleDetailRequestWire(details.Renavam, details.Chassis, details.LicensePlate, details.Color);
    }

    public static DepositAccountRequestWire ToWire(DepositAccount account)
    {
        if (account is null)
            throw new ArgumentNullException(nameof(account));

        return new DepositAccountRequestWire(
            account.HolderCpf,
            account.BankCode,
            account.Branch,
            account.BranchDigit,
            account.AccountNumber,
            account.AccountDigit,
            ToWire(account.AccountType));
    }

    public static DocumentUploadRequestWire ToWire(ProposalDocumentUpload upload)
    {
        if (upload is null)
            throw new ArgumentNullException(nameof(upload));

        if (upload.File is null)
            throw new ArgumentException("O arquivo do documento é obrigatório.", nameof(upload));

        if (!EnumMapping.IsKnown(upload.DocumentType))
            throw new ArgumentException("O tipo de documento informado não é válido.", nameof(upload));

        return new DocumentUploadRequestWire(
            ToWire(upload.Participant),
            (int)upload.DocumentType,
            upload.File.FileName,
            upload.File.Content);
    }

    public static string ToWire(ProposalParticipant participant) => participant switch
    {
        ProposalParticipant.Buyer => "COMPRADOR",
        ProposalParticipant.Seller => "VENDEDOR",
        ProposalParticipant.Store => "LOJA",
        _ => throw new ArgumentException("O participante informado não é válido.", nameof(participant))
    };

    public static string ToWire(DepositAccountType type) => type switch
    {
        DepositAccountType.Checking => "CORRENTE",
        DepositAccountType.Savings => "POUPANCA",
        DepositAccountType.Payment => "PAGAMENTO",
        _ => throw new ArgumentException("O tipo de conta informado não é válido.", nameof(type))
    };

    public static ProposalDetails ToPublic(ProposalV2ResponseWire wire)
    {
        var status = ParseStatus(wire.EnumStatus, wire.Status);

        return new ProposalDetails(
            wire.IdProposta,
            wire.Numero,
            wire.IdSimulacao,
            status,
            StatusRaw(wire.EnumStatus, status),
            wire.Status,
            EnumMapping.FromWireName<ProposalType>(wire.Tipo),
            wire.DataCriacao,
            wire.MotivoRecusa,
            wire.LinkBiometria,
            ToPublic(wire.Comprador),
            ToPublic(wire.Vendedor),
            ToPublic(wire.Loja));
    }

    public static ProposalSummary ToPublic(ProposalResponseWire wire) => new(
        wire.IdProposta,
        wire.Numero,
        wire.IdSimulacao,
        EnumMapping.FromWireName<ProposalType>(wire.Tipo),
        ParseStatus(null, wire.Status),
        wire.Status,
        wire.Comprador?.Cpf,
        wire.Vendedor?.Cpf,
        wire.DataCriacao);

    public static ProposalParticipantState? ToPublic(ProposalParticipantWire? wire)
    {
        if (wire is null)
            return null;

        return new ProposalParticipantState(
            wire.Cpf,
            wire.EnumBiometria is { } biometry ? EnumMapping.FromWireValue<BiometryStatus>(biometry) : (BiometryStatus?)null,
            wire.EnumBiometria,
            wire.Biometria,
            ToPublic(wire.StatusAtivo),
            ToPublic(wire.EtapasConcluidas),
            ToPublic(wire.Documentos));
    }

    public static ProposalParticipantState? ToPublic(ProposalStoreWire? wire)
    {
        if (wire is null)
            return null;

        return new ProposalParticipantState(
            wire.Cnpj,
            null,
            null,
            null,
            ToPublic(wire.StatusAtivo),
            ToPublic(wire.EtapasConcluidas),
            ToPublic(wire.Documentos));
    }

    private static ActiveStep? ToPublic(ActiveStepWire? wire)
    {
        if (wire is null)
            return null;

        return new ActiveStep(
            EnumMapping.FromWireValue<ProposalStep>(wire.EnumEtapa),
            wire.EnumEtapa,
            wire.Etapa,
            EnumMapping.FromWireValue<StepSituation>(wire.EnumSituacao),
            wire.EnumSituacao,
            wire.Situacao,
            EnumMapping.FromWireValue<ActionOwner>(wire.EnumResponsavelAcao),
            wire.EnumResponsavelAcao,
            wire.ResponsavelAcao);
    }

    private static IReadOnlyList<CompletedStep> ToPublic(IReadOnlyList<CompletedStepWire>? wires)
    {
        var steps = new List<CompletedStep>();

        if (wires is null)
            return steps;

        foreach (var wire in wires)
            steps.Add(new CompletedStep(EnumMapping.FromWireValue<ProposalStep>(wire.EnumEtapa), wire.EnumEtapa, wire.Etapa, wire.Data));

        return steps;
    }

    private static IReadOnlyList<ProposalDocument> ToPublic(IReadOnlyList<ProposalDocumentWire>? wires)
    {
        var documents = new List<ProposalDocument>();

        if (wires is null)
            return documents;

        foreach (var wire in wires)
        {
            documents.Add(new ProposalDocument(
                EnumMapping.FromWireValue<DocumentType>(wire.EnumTipo),
                wire.EnumTipo,
                wire.Tipo,
                EnumMapping.FromWireValue<DocumentStatus>(wire.EnumStatus),
                wire.EnumStatus,
                wire.Status,
                wire.Motivo));
        }

        return documents;
    }
}
