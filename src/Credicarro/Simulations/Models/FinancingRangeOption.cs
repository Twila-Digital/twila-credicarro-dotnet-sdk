namespace Credicarro.Simulations.Models;

/// <summary>
/// Valor mínimo de entrada para um prazo de financiamento.
/// </summary>
/// <param name="Term">Prazo em meses (<c>prazo</c>).</param>
/// <param name="MinimumDownPayment">Valor mínimo de entrada (<c>valor_minimo_entrada</c>).</param>
public sealed record FinancingRangeOption(int Term, decimal MinimumDownPayment);
