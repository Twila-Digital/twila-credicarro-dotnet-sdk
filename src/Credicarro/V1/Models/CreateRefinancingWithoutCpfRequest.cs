using Credicarro.Internal;

namespace Credicarro.V1.Models;

/// <summary>
/// Simulação de refinanciamento sem CPF (<c>POST v1/proposal/simulation/refinancing/without-cpf</c>): usa a loja de integração do parceiro e não grava nada.
/// </summary>
/// <param name="LicensePlate">Placa, no padrão antigo (ABC-1234) ou Mercosul (ABC1D23) (<c>placa</c>).</param>
/// <param name="RequestedAmount">Valor solicitado em reais, maior que zero (<c>valorSolicitado</c>).</param>
/// <param name="State">UF do registro do veículo, com 2 letras (<c>uf</c>). Define as taxas e o custo de registro.</param>
public sealed record CreateRefinancingWithoutCpfRequest(
    string LicensePlate,
    decimal RequestedAmount,
    string State)
{
    public override string ToString() =>
        $"CreateRefinancingWithoutCpfRequest {{ LicensePlate = {Masking.Hidden}, RequestedAmount = {RequestedAmount}, State = {State} }}";
}
