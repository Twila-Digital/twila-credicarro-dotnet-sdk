using Credicarro.Serialization;

namespace Credicarro.Simulations.Models;

/// <summary>
/// A quem a faixa de retorno de comissão se aplica (<c>alvo</c>), lido pelo nome.
/// </summary>
public enum CommissionReturnTarget
{
    Store = 1,
    Organization = 2,

    [UnknownValue]
    Unknown = -1
}

/// <summary>
/// Faixa de retorno de comissão configurada para a loja. A simulação valida pela faixa com
/// <see cref="Target"/> <see cref="CommissionReturnTarget.Store"/>.
/// </summary>
/// <param name="Type">Modalidade da faixa (<c>tipo</c>): <see cref="SimulationType.Financing"/> ou <see cref="SimulationType.Refinancing"/>.</param>
/// <param name="Target">A quem a faixa se aplica (<c>alvo</c>).</param>
/// <param name="MinimumReturn">Menor <c>retornoComissao</c> aceito (<c>retorno_minimo</c>).</param>
/// <param name="MaximumReturn">Maior <c>retornoComissao</c> aceito (<c>retorno_maximo</c>).</param>
/// <param name="AllowsDefaultRate">Indica se a loja permite usar a taxa padrão (<c>permite_taxa_padrao</c>).</param>
public sealed record CommissionReturn(
    SimulationType Type,
    CommissionReturnTarget Target,
    int MinimumReturn,
    int MaximumReturn,
    bool AllowsDefaultRate);
