using Credicarro.Internal;

namespace Credicarro.V1.Models;

/// <summary>
/// Simulação de refinanciamento na versão 1 (<c>POST v1/proposal/simulation/refinancing</c>): a loja não é informada.
/// </summary>
/// <param name="Cpf">CPF do cliente (<c>cpf</c>).</param>
/// <param name="LicensePlate">Placa (<c>placa</c>).</param>
/// <param name="RequestedAmount">Valor solicitado (<c>valorSolicitado</c>).</param>
public sealed record CreateRefinancingSimulationV1Request(string Cpf, string LicensePlate, decimal RequestedAmount)
{
    public override string ToString() =>
        $"CreateRefinancingSimulationV1Request {{ Cpf = {Masking.Cpf(Cpf)}, LicensePlate = {Masking.Hidden}, RequestedAmount = {RequestedAmount} }}";
}
