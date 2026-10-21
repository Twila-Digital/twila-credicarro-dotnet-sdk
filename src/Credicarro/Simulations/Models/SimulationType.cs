using Credicarro.Serialization;

namespace Credicarro.Simulations.Models;

/// <summary>
/// Tipo da simulação (<c>tipo</c>), lido pelo nome.
/// </summary>
public enum SimulationType
{
    Financing = 1,
    Refinancing = 2,
    AmountSimulation = 3,

    [UnknownValue]
    Unknown = -1
}
