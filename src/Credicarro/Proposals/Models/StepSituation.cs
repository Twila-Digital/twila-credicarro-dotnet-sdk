using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Situação da etapa (<c>enum_situacao</c> / <c>situacao</c>).
/// </summary>
public enum StepSituation
{
    AwaitingSubmission = 1,
    AwaitingCustomer = 2,
    Processing = 3,
    PendingCorrection = 4,
    Blocked = 5,
    Completed = 6,
    Canceled = 7,

    [UnknownValue]
    Unknown = -1
}
