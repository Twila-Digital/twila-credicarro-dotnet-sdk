using System.Text.Json.Serialization;

namespace Credicarro.Internal.Generated.Simulations;

internal sealed record FinancingRangeResponseWire(
    [property: JsonPropertyName("opcoes_por_prazo")] IReadOnlyList<FinancingRangeOptionWire>? OpcoesPorPrazo);

internal sealed record FinancingRangeOptionWire(
    [property: JsonPropertyName("prazo")] int Prazo,
    [property: JsonPropertyName("valor_minimo_entrada")] decimal ValorMinimoEntrada);

internal sealed record RefinancingRangeResponseWire(
    [property: JsonPropertyName("opcoes_por_prazo")] IReadOnlyList<RefinancingRangeOptionWire>? OpcoesPorPrazo);

internal sealed record RefinancingRangeOptionWire(
    [property: JsonPropertyName("prazo")] int Prazo,
    [property: JsonPropertyName("valor_minimo_refinanciavel")] decimal ValorMinimoRefinanciavel,
    [property: JsonPropertyName("valor_maximo_refinanciavel")] decimal ValorMaximoRefinanciavel);

internal sealed record CreateFinancingSimulationRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("placa")] string Placa,
    [property: JsonPropertyName("valorVeiculo")] decimal ValorVeiculo,
    [property: JsonPropertyName("valorEntrada")] decimal ValorEntrada,
    [property: JsonPropertyName("uf")] string Uf,
    [property: JsonPropertyName("isVeiculoZeroKm")] bool IsVeiculoZeroKm,
    [property: JsonPropertyName("retornoComissao")] int? RetornoComissao,
    [property: JsonPropertyName("cnpjLoja")] string CnpjLoja);

internal sealed record CreateRefinancingSimulationRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("placa")] string Placa,
    [property: JsonPropertyName("valorSolicitado")] decimal ValorSolicitado,
    [property: JsonPropertyName("retornoComissao")] int? RetornoComissao,
    [property: JsonPropertyName("cnpjLoja")] string CnpjLoja);

internal sealed record CreateFinancingSimulationV1RequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("placa")] string Placa,
    [property: JsonPropertyName("valorVeiculo")] decimal ValorVeiculo,
    [property: JsonPropertyName("valorEntrada")] decimal ValorEntrada,
    [property: JsonPropertyName("uf")] string Uf,
    [property: JsonPropertyName("isVeiculoZeroKm")] bool IsVeiculoZeroKm);

internal sealed record CreateRefinancingSimulationV1RequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("placa")] string Placa,
    [property: JsonPropertyName("valorSolicitado")] decimal ValorSolicitado);

internal sealed record SimulationListResponseWire(
    [property: JsonPropertyName("simulacoes")] IReadOnlyList<SimulationOptionWire>? Simulacoes);

internal sealed record CommissionReturnWire(
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("alvo")] string? Alvo,
    [property: JsonPropertyName("retorno_minimo")] int RetornoMinimo,
    [property: JsonPropertyName("retorno_maximo")] int RetornoMaximo,
    [property: JsonPropertyName("permite_taxa_padrao")] bool PermiteTaxaPadrao);

internal sealed record ListCommissionReturnsResponseWire(
    [property: JsonPropertyName("retornos_comissao")] IReadOnlyList<CommissionReturnWire>? RetornosComissao);

internal sealed record SimulationOptionWire(
    [property: JsonPropertyName("id_simulacao")] Guid IdSimulacao,
    [property: JsonPropertyName("valor_solicitado")] decimal ValorSolicitado,
    [property: JsonPropertyName("prazo")] int Prazo,
    [property: JsonPropertyName("valor_parcela")] decimal ValorParcela,
    [property: JsonPropertyName("valor_tac")] decimal ValorTac,
    [property: JsonPropertyName("valor_boleto")] decimal ValorBoleto,
    [property: JsonPropertyName("valor_seguro")] decimal ValorSeguro,
    [property: JsonPropertyName("valor_iof")] decimal ValorIof,
    [property: JsonPropertyName("percentual_iof")] decimal PercentualIof,
    [property: JsonPropertyName("valor_credito_total")] decimal ValorCreditoTotal,
    [property: JsonPropertyName("valor_divida_total")] decimal ValorDividaTotal,
    [property: JsonPropertyName("percentual_cet_mensal")] decimal PercentualCetMensal,
    [property: JsonPropertyName("percentual_cet_anual")] decimal PercentualCetAnual,
    [property: JsonPropertyName("percentual_juros_mensal")] decimal PercentualJurosMensal,
    [property: JsonPropertyName("percentual_juros_anual")] decimal PercentualJurosAnual,
    [property: JsonPropertyName("valor_retencao_diaria_aproximado")] decimal? ValorRetencaoDiariaAproximado);

internal sealed record SimulationWire(
    [property: JsonPropertyName("id_simulacao")] Guid IdSimulacao,
    [property: JsonPropertyName("prazo")] long Prazo,
    [property: JsonPropertyName("valor_parcela")] decimal ValorParcela,
    [property: JsonPropertyName("valor_solicitado")] decimal ValorSolicitado,
    [property: JsonPropertyName("valor_entrada")] decimal ValorEntrada,
    [property: JsonPropertyName("valor_bem")] decimal ValorBem,
    [property: JsonPropertyName("valor_tac")] decimal ValorTac,
    [property: JsonPropertyName("taxa_juros_negociada")] decimal TaxaJurosNegociada,
    [property: JsonPropertyName("valor_registro")] decimal ValorRegistro,
    [property: JsonPropertyName("uf")] string? Uf,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("transacao")] SimulationTransactionWire? Transacao,
    [property: JsonPropertyName("juros")] SimulationInterestWire? Juros,
    [property: JsonPropertyName("cet")] SimulationCetWire? Cet,
    [property: JsonPropertyName("iof")] SimulationIofWire? Iof,
    [property: JsonPropertyName("veiculo")] SimulationVehicleWire? Veiculo,
    [property: JsonPropertyName("cliente")] SimulationCustomerWire? Cliente,
    [property: JsonPropertyName("data_criacao")] DateTimeOffset? DataCriacao);

internal sealed record SimulationTransactionWire(
    [property: JsonPropertyName("valor_total_credito")] decimal ValorTotalCredito,
    [property: JsonPropertyName("valor_total_debito")] decimal ValorTotalDebito);

internal sealed record SimulationInterestWire(
    [property: JsonPropertyName("percentual_juros_mensal")] decimal PercentualJurosMensal,
    [property: JsonPropertyName("percentual_juros_anual")] decimal PercentualJurosAnual);

internal sealed record SimulationCetWire(
    [property: JsonPropertyName("percentual_mensal")] decimal PercentualMensal,
    [property: JsonPropertyName("percentual_anual")] decimal PercentualAnual);

internal sealed record SimulationIofWire(
    [property: JsonPropertyName("valor")] decimal Valor,
    [property: JsonPropertyName("percentual")] decimal Percentual);

internal sealed record SimulationVehicleWire(
    [property: JsonPropertyName("marca")] string? Marca,
    [property: JsonPropertyName("modelo")] string? Modelo,
    [property: JsonPropertyName("versao")] string? Versao,
    [property: JsonPropertyName("ano")] int? Ano,
    [property: JsonPropertyName("valor")] decimal? Valor,
    [property: JsonPropertyName("placa")] string? Placa,
    [property: JsonPropertyName("veiculo_zero_km")] bool? VeiculoZeroKm,
    [property: JsonPropertyName("chassi")] string? Chassi,
    [property: JsonPropertyName("renavam")] string? Renavam,
    [property: JsonPropertyName("cor")] string? Cor);

internal sealed record SimulationCustomerWire(
    [property: JsonPropertyName("cpf")] string? Cpf);
