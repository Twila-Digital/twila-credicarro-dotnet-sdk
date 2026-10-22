using Credicarro.Internal;

namespace Credicarro.Simulations.Models;

/// <summary>
/// Simulação de financiamento (<c>POST v2/proposal/simulation/financing</c>). O cliente precisa estar cadastrado.
/// </summary>
/// <param name="Cpf">CPF do cliente (<c>cpf</c>).</param>
/// <param name="LicensePlate">Placa, no padrão antigo (ABC-1234) ou Mercosul (ABC1D23) (<c>placa</c>).</param>
/// <param name="VehicleAmount">Valor do veículo em reais, maior que zero (<c>valorVeiculo</c>).</param>
/// <param name="DownPayment">Valor de entrada em reais, zero quando não houver; menor que o valor do veículo (<c>valorEntrada</c>).</param>
/// <param name="State">UF do registro do veículo (<c>uf</c>).</param>
/// <param name="IsZeroKm"><see langword="true"/> para veículo zero quilômetro (<c>isVeiculoZeroKm</c>).</param>
/// <param name="StoreCnpj">CNPJ da loja de integração vinculada ao parceiro, formatado ou só números (<c>cnpjLoja</c>).</param>
public sealed record CreateFinancingSimulationRequest(
    string Cpf,
    string LicensePlate,
    decimal VehicleAmount,
    decimal DownPayment,
    string State,
    bool IsZeroKm,
    string StoreCnpj)
{
    public override string ToString() =>
        $"CreateFinancingSimulationRequest {{ Cpf = {Masking.Cpf(Cpf)}, LicensePlate = {Masking.Hidden}, VehicleAmount = {VehicleAmount}, DownPayment = {DownPayment}, State = {State}, IsZeroKm = {IsZeroKm}, StoreCnpj = {StoreCnpj} }}";
}
