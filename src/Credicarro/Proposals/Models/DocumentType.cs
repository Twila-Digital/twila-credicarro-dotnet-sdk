using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Tipo de documento (<c>tipoDocumento</c> no envio; <c>enum_tipo</c> / <c>tipo</c> na consulta).
/// A Credicarro pode anexar outros tipos; na leitura eles viram <see cref="Unknown"/>, e o código bruto fica em
/// <see cref="ProposalDocument.TypeRaw"/>.
/// </summary>
public enum DocumentType
{
    Cnh = 1,
    Cpf = 2,
    Rg = 3,
    ProofOfIncome = 4,
    ProofOfAddress = 5,
    VehicleDocument = 6,
    DigitalCnh = 7,
    EcvReport = 8,
    InspectionReport = 9,
    CnpjCard = 11,
    Atpv = 57,

    [UnknownValue]
    Unknown = -1
}
