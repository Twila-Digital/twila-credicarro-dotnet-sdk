using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Situação da biometria (<c>enum_biometria</c> / <c>biometria</c>).
/// </summary>
public enum BiometryStatus
{
    NotStarted = 0,
    SessionCreated = 1,
    InAnalysis = 2,
    Approved = 3,
    Rejected = 4,

    [UnknownValue]
    Unknown = -1
}
