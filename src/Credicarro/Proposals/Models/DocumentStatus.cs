using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Status do documento (<c>enum_status</c> / <c>status</c> dentro de <c>documentos</c>).
/// <see cref="Pending"/> indica documento exigido que ainda não foi enviado (só aparece na consulta).
/// </summary>
public enum DocumentStatus
{
    Received = 0,
    Valid = 1,
    Invalid = 2,
    InAnalysis = 3,
    Deleted = 4,
    Pending = 5,

    [UnknownValue]
    Unknown = -1
}
