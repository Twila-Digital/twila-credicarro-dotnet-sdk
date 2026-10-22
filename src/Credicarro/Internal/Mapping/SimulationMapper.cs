using Credicarro.Internal.Generated.Simulations;
using Credicarro.Serialization;
using Credicarro.Simulations.Models;
using Credicarro.V1.Models;

namespace Credicarro.Internal.Mapping;

internal static class SimulationMapper
{
    public static FinancingRangeOption ToPublic(FinancingRangeOptionWire wire) =>
        new(wire.Prazo, wire.ValorMinimoEntrada);

    public static RefinancingRangeOption ToPublic(RefinancingRangeOptionWire wire) =>
        new(wire.Prazo, wire.ValorMinimoRefinanciavel, wire.ValorMaximoRefinanciavel);

    public static CreateFinancingSimulationRequestWire ToWire(CreateFinancingSimulationRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateFinancingSimulationRequestWire(
            request.Cpf,
            request.LicensePlate,
            request.VehicleAmount,
            request.DownPayment,
            request.State,
            request.IsZeroKm,
            request.CommissionReturn,
            request.StoreCnpj);
    }

    public static CreateFinancingSimulationV1RequestWire ToWire(CreateFinancingSimulationV1Request request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateFinancingSimulationV1RequestWire(
            request.Cpf,
            request.LicensePlate,
            request.VehicleAmount,
            request.DownPayment,
            request.State,
            request.IsZeroKm);
    }

    public static CreateRefinancingSimulationRequestWire ToWire(CreateRefinancingSimulationRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateRefinancingSimulationRequestWire(
            request.Cpf, request.LicensePlate, request.RequestedAmount, request.CommissionReturn, request.StoreCnpj);
    }

    public static CreateRefinancingSimulationV1RequestWire ToWire(CreateRefinancingSimulationV1Request request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateRefinancingSimulationV1RequestWire(request.Cpf, request.LicensePlate, request.RequestedAmount);
    }

    public static CommissionReturn ToPublic(CommissionReturnWire wire) => new(
        EnumMapping.FromWireName<CommissionReturnType>(wire.Tipo),
        EnumMapping.FromWireName<CommissionReturnTarget>(wire.Alvo),
        wire.RetornoMinimo,
        wire.RetornoMaximo,
        wire.PermiteTaxaPadrao);

    public static SimulationOption ToPublic(SimulationOptionWire wire) => new(
        wire.IdSimulacao,
        wire.ValorSolicitado,
        wire.Prazo,
        wire.ValorParcela,
        wire.ValorTac,
        wire.ValorBoleto,
        wire.ValorSeguro,
        wire.ValorIof,
        wire.PercentualIof,
        wire.ValorCreditoTotal,
        wire.ValorDividaTotal,
        wire.PercentualCetMensal,
        wire.PercentualCetAnual,
        wire.PercentualJurosMensal,
        wire.PercentualJurosAnual,
        wire.ValorRetencaoDiariaAproximado);

    public static Simulation ToPublic(SimulationWire wire) => new(
        wire.IdSimulacao,
        checked((int)wire.Prazo),
        wire.ValorParcela,
        wire.ValorSolicitado,
        wire.ValorEntrada,
        wire.ValorBem,
        wire.ValorTac,
        wire.TaxaJurosNegociada,
        wire.ValorRegistro,
        wire.Uf,
        EnumMapping.FromWireName<SimulationType>(wire.Tipo),
        wire.Transacao is null ? null : new SimulationTransaction(wire.Transacao.ValorTotalCredito, wire.Transacao.ValorTotalDebito),
        wire.Juros is null ? null : new SimulationInterest(wire.Juros.PercentualJurosMensal, wire.Juros.PercentualJurosAnual),
        wire.Cet is null ? null : new SimulationCet(wire.Cet.PercentualMensal, wire.Cet.PercentualAnual),
        wire.Iof is null ? null : new SimulationIof(wire.Iof.Valor, wire.Iof.Percentual),
        wire.Veiculo is null
            ? null
            : new SimulationVehicle(
                wire.Veiculo.Marca,
                wire.Veiculo.Modelo,
                wire.Veiculo.Versao,
                wire.Veiculo.Ano,
                wire.Veiculo.Valor,
                wire.Veiculo.Placa,
                wire.Veiculo.VeiculoZeroKm,
                wire.Veiculo.Chassi,
                wire.Veiculo.Renavam,
                wire.Veiculo.Cor),
        wire.Cliente?.Cpf,
        wire.DataCriacao);
}
