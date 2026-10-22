using Credicarro.Internal;

namespace Credicarro.Simulations.Models;

/// <summary>
/// Simulação de refinanciamento (<c>POST v2/proposal/simulation/refinancing</c>). O cliente precisa estar cadastrado
/// (com endereço, quando a UF do veículo não for identificada) e o valor precisa estar na faixa refinanciável da placa.
/// </summary>
/// <param name="Cpf">CPF do cliente (<c>cpf</c>).</param>
/// <param name="LicensePlate">Placa (<c>placa</c>).</param>
/// <param name="RequestedAmount">Valor solicitado em reais (<c>valorSolicitado</c>).</param>
/// <param name="StoreCnpj">CNPJ da loja de integração (<c>cnpjLoja</c>).</param>
public sealed record CreateRefinancingSimulationRequest(
    string Cpf,
    string LicensePlate,
    decimal RequestedAmount,
    string StoreCnpj)
{
    public override string ToString() =>
        $"CreateRefinancingSimulationRequest {{ Cpf = {Masking.Cpf(Cpf)}, LicensePlate = {Masking.Hidden}, RequestedAmount = {RequestedAmount}, StoreCnpj = {StoreCnpj} }}";
}
