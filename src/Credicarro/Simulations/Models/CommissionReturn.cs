namespace Credicarro.Simulations.Models;

/// <summary>
/// Faixa de retorno de comissão configurada para a loja, por tipo de simulação
/// (<c>GET v2/proposal/simulation/commission-returns</c>).
/// </summary>
/// <param name="Type">Tipo de simulação ao qual o retorno se aplica (<c>tipo</c>).</param>
/// <param name="Target">A quem pertence a configuração de comissão (<c>alvo</c>).</param>
/// <param name="MinimumReturn">Menor retorno de comissão aceito para esse tipo (<c>retorno_minimo</c>).</param>
/// <param name="MaximumReturn">Maior retorno de comissão aceito para esse tipo (<c>retorno_maximo</c>).</param>
/// <param name="AllowDefaultRate">
/// Indica se a loja aceita simular sem informar <c>CommissionReturn</c>, usando a taxa padrão (<c>permite_taxa_padrao</c>).
/// </param>
public sealed record CommissionReturn(
    CommissionReturnType Type,
    CommissionReturnTarget Target,
    int MinimumReturn,
    int MaximumReturn,
    bool AllowDefaultRate);
