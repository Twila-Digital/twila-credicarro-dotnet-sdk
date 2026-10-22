using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Dados do veículo (<c>POST v2/proposal/{id}/vehicle-detail</c>), aceitos enquanto a etapa <c>DADOS_VEICULO</c> estiver ativa.
/// </summary>
/// <param name="Renavam">Renavam, até 11 dígitos com dígito verificador válido (<c>renavam</c>).</param>
/// <param name="Chassis">Chassi de 17 caracteres, sem as letras I, O e Q (<c>chassi</c>).</param>
/// <param name="LicensePlate">Placa (<c>placa</c>).</param>
/// <param name="Color">Cor, sem números, até 30 caracteres (<c>cor</c>).</param>
public sealed record VehicleDetails(string Renavam, string Chassis, string LicensePlate, string Color)
{
    public override string ToString() =>
        $"VehicleDetails {{ Renavam = {Masking.Hidden}, Chassis = {Masking.Hidden}, LicensePlate = {Masking.Hidden}, Color = {Masking.Hidden} }}";
}
