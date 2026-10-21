using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Modalidade da proposta (<c>tipo</c>), lida pelo nome.
/// </summary>
public enum ProposalType
{
    Financing = 1,
    Refinancing = 2,

    [UnknownValue]
    Unknown = -1
}
