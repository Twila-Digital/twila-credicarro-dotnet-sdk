using Credicarro.Internal;

namespace Credicarro.Simulations.Models;

/// <summary>
/// Simulação consultada (<c>GET v1/proposal/simulation/{id}</c> e listagem).
/// </summary>
public sealed record Simulation(
    Guid SimulationId,
    int Term,
    decimal InstallmentAmount,
    decimal RequestedAmount,
    decimal DownPayment,
    decimal AssetAmount,
    decimal TacAmount,
    decimal NegotiatedInterestRate,
    decimal RegistrationAmount,
    string? State,
    SimulationType Type,
    SimulationTransaction? Transaction,
    SimulationInterest? Interest,
    SimulationCet? Cet,
    SimulationIof? Iof,
    SimulationVehicle? Vehicle,
    string? CustomerCpf,
    DateTimeOffset? CreatedAt)
{
    /// <summary>Sem o CPF do cliente nem os dados do veículo.</summary>
    public override string ToString() =>
        $"Simulation {{ SimulationId = {SimulationId}, Term = {Term}, InstallmentAmount = {InstallmentAmount}, RequestedAmount = {RequestedAmount}, Type = {Type}, CustomerCpf = {Masking.Cpf(CustomerCpf)}, Vehicle = {Vehicle} }}";
}

/// <summary>Totais de crédito e débito (<c>transacao</c>).</summary>
public sealed record SimulationTransaction(decimal TotalCredit, decimal TotalDebit);

/// <summary>Juros mensal e anual (<c>juros</c>).</summary>
public sealed record SimulationInterest(decimal MonthlyRate, decimal AnnualRate);

/// <summary>Custo Efetivo Total mensal e anual (<c>cet</c>).</summary>
public sealed record SimulationCet(decimal MonthlyRate, decimal AnnualRate);

/// <summary>IOF (<c>iof</c>).</summary>
public sealed record SimulationIof(decimal Amount, decimal Rate);

/// <summary>Veículo da simulação (<c>veiculo</c>).</summary>
public sealed record SimulationVehicle(
    string? Brand,
    string? Model,
    string? Version,
    int? Year,
    decimal? Value,
    string? LicensePlate,
    bool? IsZeroKm,
    string? Chassis,
    string? Renavam,
    string? Color)
{
    /// <summary>Identificadores do veículo mascarados.</summary>
    public override string ToString() =>
        $"SimulationVehicle {{ Brand = {Brand}, Model = {Model}, Year = {Year}, LicensePlate = {Masking.Hidden}, Chassis = {Masking.Hidden}, Renavam = {Masking.Hidden}, Color = {Color} }}";
}
