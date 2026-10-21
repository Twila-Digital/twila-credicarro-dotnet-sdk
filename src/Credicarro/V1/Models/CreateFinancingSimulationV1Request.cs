using Credicarro.Internal;

namespace Credicarro.V1.Models;

/// <summary>
/// Simulação de financiamento na versão 1 (<c>POST v1/proposal/simulation/financing</c>): a loja não é informada.
/// </summary>
/// <param name="Cpf">CPF do cliente (<c>cpf</c>).</param>
/// <param name="LicensePlate">Placa (<c>placa</c>).</param>
/// <param name="VehicleAmount">Valor do veículo (<c>valorVeiculo</c>).</param>
/// <param name="DownPayment">Valor de entrada (<c>valorEntrada</c>).</param>
/// <param name="State">UF do registro do veículo (<c>uf</c>).</param>
/// <param name="IsZeroKm">Veículo zero quilômetro (<c>isVeiculoZeroKm</c>).</param>
public sealed record CreateFinancingSimulationV1Request(
    string Cpf,
    string LicensePlate,
    decimal VehicleAmount,
    decimal DownPayment,
    string State,
    bool IsZeroKm)
{
    public override string ToString() =>
        $"CreateFinancingSimulationV1Request {{ Cpf = {Masking.Cpf(Cpf)}, LicensePlate = {Masking.Hidden}, VehicleAmount = {VehicleAmount}, DownPayment = {DownPayment}, State = {State}, IsZeroKm = {IsZeroKm} }}";
}
