using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Quem precisa agir na etapa (<c>enum_responsavel_acao</c> / <c>responsavel_acao</c>).
/// <see cref="Integrator"/> significa que o seu sistema precisa enviar algo pela API.
/// </summary>
public enum ActionOwner
{
    None = 0,
    Customer = 1,
    Integrator = 2,
    Credicarro = 3,

    [UnknownValue]
    Unknown = -1
}
