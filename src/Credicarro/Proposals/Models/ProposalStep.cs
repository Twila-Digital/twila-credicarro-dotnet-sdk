using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Etapa do participante (<c>enum_etapa</c> / <c>etapa</c>).
/// </summary>
public enum ProposalStep
{
    Undefined = 0,
    ProposalCreated = 1,
    ProposalApproved = 2,
    SellerNotification = 3,
    BiometryAnalysis = 4,
    DepositAccount = 5,
    DocumentSubmission = 6,
    DocumentCorrection = 7,
    VehicleDetails = 8,
    Inspection = 9,
    Signature = 10,
    Payment = 11,
    ProposalCompleted = 12,

    [UnknownValue]
    Unknown = -1
}
