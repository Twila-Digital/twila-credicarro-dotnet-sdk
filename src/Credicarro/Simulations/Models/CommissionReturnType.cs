using Credicarro.Serialization;

namespace Credicarro.Simulations.Models;

/// <summary>
/// Tipo de simulação ao qual o retorno de comissão se aplica (<c>tipo</c>), lido pelo nome.
/// </summary>
public enum CommissionReturnType
{
    Financing = 1,
    Refinancing = 2,

    [UnknownValue]
    Unknown = -1
}
