namespace Credicarro.Simulations.Models;

/// <summary>
/// Faixa de valor refinanciável para um prazo.
/// </summary>
/// <param name="Term">Prazo (<c>prazo</c>).</param>
/// <param name="MinimumAmount">Valor mínimo refinanciável (<c>valor_minimo_refinanciavel</c>).</param>
/// <param name="MaximumAmount">Valor máximo refinanciável (<c>valor_maximo_refinanciavel</c>).</param>
public sealed record RefinancingRangeOption(int Term, decimal MinimumAmount, decimal MaximumAmount);
