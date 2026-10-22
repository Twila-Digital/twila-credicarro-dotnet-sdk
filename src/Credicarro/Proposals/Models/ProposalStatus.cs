using Credicarro.Serialization;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Status da proposta. Na consulta e no webhook detalhado vem em português (<c>EM_ANALISE</c>, <c>APROVADA</c>...);
/// na listagem e no webhook simples vem em inglês (<c>Analysing</c>, <c>Approved</c>...). O código numérico é o mesmo.
/// </summary>
public enum ProposalStatus
{
    /// <summary><c>EM_ANALISE</c> / <c>Analysing</c></summary>
    Analysing = 1,

    /// <summary><c>APROVADA</c> / <c>Approved</c></summary>
    Approved = 2,

    /// <summary><c>RECUSADA</c> / <c>Refused</c></summary>
    Refused = 3,

    /// <summary><c>CANCELADA</c> / <c>Canceled</c></summary>
    Canceled = 4,

    /// <summary><c>ANALISE_MANUAL</c> / <c>ManualAnalysis</c></summary>
    ManualAnalysis = 5,

    /// <summary><c>FINALIZADA</c> / <c>Completed</c></summary>
    Completed = 6,

    /// <summary><c>EM_NEGOCIACAO</c> / <c>InNegotiation</c></summary>
    InNegotiation = 7,

    /// <summary><c>EXPIRADA</c> / <c>Expired</c></summary>
    Expired = 8,

    /// <summary><c>PENDENTE</c> / <c>Pending</c></summary>
    Pending = 9,

    [UnknownValue]
    Unknown = -1
}
