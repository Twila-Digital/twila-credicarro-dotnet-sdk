namespace Credicarro.V1.Models;

/// <summary>
/// Uma opção da simulação de refinanciamento sem CPF (um prazo). Não tem identificador e não serve para criar proposta.
/// <c>MinimumAmount</c> e <c>MaximumAmount</c> são a faixa refinanciável do prazo.
/// </summary>
public sealed record RefinancingWithoutCpfOption(
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
    decimal MinimumAmount,
    decimal MaximumAmount);
