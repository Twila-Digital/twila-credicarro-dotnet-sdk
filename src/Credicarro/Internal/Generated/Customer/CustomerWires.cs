using System.Text.Json.Serialization;

namespace Credicarro.Internal.Generated.Customer;

internal sealed record CreateCustomerRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("sobrenome")] string Sobrenome,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("celular")] string Celular,
    [property: JsonPropertyName("dataDeNascimento")] DateTimeOffset DataDeNascimento);

internal sealed record UpsertCustomerRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("sobrenome")] string Sobrenome,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("celular")] string Celular,
    [property: JsonPropertyName("dataDeNascimento")] DateTimeOffset DataDeNascimento,
    [property: JsonPropertyName("endereco")] AddressWire Endereco,
    [property: JsonPropertyName("dadosProfissionais")] ProfessionalDataWire DadosProfissionais);

internal sealed record AddressWire(
    [property: JsonPropertyName("rua")] string Rua,
    [property: JsonPropertyName("numero")] string? Numero,
    [property: JsonPropertyName("complemento")] string? Complemento,
    [property: JsonPropertyName("bairro")] string Bairro,
    [property: JsonPropertyName("cidade")] string Cidade,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("cep")] string Cep);

internal sealed record ProfessionalDataWire(
    [property: JsonPropertyName("tipoOcupacao")] int TipoOcupacao,
    [property: JsonPropertyName("renda")] decimal Renda);

internal sealed record UpsertAddressRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("endereco")] AddressWire Endereco);

internal sealed record UpsertProfessionalRequestWire(
    [property: JsonPropertyName("cpf")] string Cpf,
    [property: JsonPropertyName("tipoOcupacao")] int TipoOcupacao,
    [property: JsonPropertyName("renda")] decimal Renda);

internal sealed record CustomerIdResponseWire(
    [property: JsonPropertyName("id_cliente")] Guid IdCliente);
