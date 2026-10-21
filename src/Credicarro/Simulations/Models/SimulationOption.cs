namespace Credicarro.Simulations.Models;

/// <summary>
/// Uma opção de simulação (um prazo). Use <see cref="SimulationId"/> para criar a proposta.
/// </summary>
public sealed record SimulationOption(
    Guid SimulationId,
    decimal RequestedAmount,
    int Term,
    decimal InstallmentAmount,
    decimal TacAmount,
    decimal BoletoAmount,
    decimal InsuranceAmount,
    decimal IofAmount,
    decimal IofRate,
    decimal TotalCreditAmount,
    decimal TotalDebtAmount,
    decimal MonthlyCet,
    decimal AnnualCet,
    decimal MonthlyInterestRate,
    decimal AnnualInterestRate,
    decimal? ApproximateDailyRetention);
