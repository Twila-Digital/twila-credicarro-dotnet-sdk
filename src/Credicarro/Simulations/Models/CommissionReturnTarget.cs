using Credicarro.Serialization;

namespace Credicarro.Simulations.Models;

/// <summary>
/// A quem pertence a configuração de comissão (<c>alvo</c>), lido pelo nome.
/// </summary>
public enum CommissionReturnTarget
{
    Store = 1,
    Organization = 2,

    [UnknownValue]
    Unknown = -1
}
