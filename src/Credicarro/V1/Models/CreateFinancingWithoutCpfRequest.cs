using Credicarro.Internal;

namespace Credicarro.V1.Models;

/// <summary>
/// Simulação de financiamento sem CPF (<c>POST v1/proposal/simulation/financing/without-cpf</c>): usa a loja de integração do parceiro e não grava nada.
/// </summary>
/// <param name="LicensePlate">Placa, no padrão antigo (ABC-1234) ou Mercosul (ABC1D23) (<c>placa</c>).</param>
/// <param name="VehicleAmount">Valor do veículo em reais, maior que zero (<c>valorVeiculo</c>).</param>
/// <param name="State">UF do registro do veículo, com 2 letras (<c>uf</c>).</param>
/// <param name="DownPayment">Valor de entrada em reais (<c>valorEntrada</c>). Opcional: quando omitido, a API usa a entrada mínima da taxa da loja.</param>
public sealed record CreateFinancingWithoutCpfRequest(
    string LicensePlate,
    decimal VehicleAmount,
    string State,
    decimal? DownPayment = null)
{
    public override string ToString() =>
        $"CreateFinancingWithoutCpfRequest {{ LicensePlate = {Masking.Hidden}, VehicleAmount = {VehicleAmount}, State = {State}, DownPayment = {DownPayment} }}";
}
