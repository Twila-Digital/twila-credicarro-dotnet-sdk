namespace Credicarro.V1.Models;

/// <summary>
/// Uma opção da simulação de financiamento sem CPF (um prazo). Não tem identificador e não serve para criar proposta.
/// </summary>
public sealed record FinancingWithoutCpfOption(
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
    decimal? ApproximateDailyRetention,
    decimal MinimumDownPayment);
