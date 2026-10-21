using Credicarro.Internal;

namespace Credicarro.Customers.Models;

/// <summary>
/// Dados profissionais do cliente.
/// </summary>
/// <param name="Occupation">Tipo de ocupação (<c>tipoOcupacao</c>).</param>
/// <param name="MonthlyIncome">Renda mensal em reais, maior que zero (<c>renda</c>).</param>
public sealed record ProfessionalData(OccupationType Occupation, decimal MonthlyIncome)
{
    public override string ToString() =>
        $"ProfessionalData {{ Occupation = {Occupation}, MonthlyIncome = {Masking.Hidden} }}";
}
