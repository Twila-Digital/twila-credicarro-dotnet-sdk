using Credicarro.Customers.Models;

namespace Credicarro.Customers;

/// <summary>
/// Cadastro do cliente (comprador). O cliente é identificado pelo CPF em todas as chamadas.
/// </summary>
public interface ICustomersClient
{
    /// <summary>
    /// Cadastro completo em uma chamada (<c>POST v1/customer/unified</c>): dados pessoais, endereço e dados profissionais.
    /// Recomendado, porque já deixa o cliente pronto para a proposta.
    /// <para>
    /// <b>Atenção à <paramref name="idempotencyKey"/>:</b> o SDK <b>nunca</b> gera chave para esta rota, e ela muda o
    /// comportamento. <b>Sem chave</b> (padrão), cria o cliente ou atualiza o cadastro se o CPF já existir; como é um
    /// upsert sem chave, falhas de rede <b>não são repetidas automaticamente</b>. <b>Com chave</b>, a rota só cria (CPF
    /// existente devolve <c>409 Cpf.CpfAlreadyAdded</c>) e o SDK repete a chamada com a mesma chave em falhas transitórias.
    /// </para>
    /// </summary>
    /// <param name="request">Dados completos do cliente.</param>
    /// <param name="idempotencyKey">Chave opcional (1 a 200 caracteres). Passe uma só quando quiser apenas criar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>O identificador do cliente (<c>id_cliente</c>).</returns>
    Task<Guid> UpsertAsync(UpsertCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria o cliente só com os dados pessoais (<c>POST v1/customer</c>). CPF, e-mail e celular já cadastrados
    /// devolvem <c>409</c>. A <c>Idempotency-Key</c> é automática (ou a sua, em <paramref name="idempotencyKey"/>) e repetida em todas as tentativas.
    /// </summary>
    Task<Guid> CreateAsync(CreateCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria ou atualiza o endereço de um cliente já cadastrado (<c>POST v1/customer/address</c>, resposta <c>202</c>).
    /// </summary>
    Task UpsertAddressAsync(string cpf, CustomerAddress address, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria ou atualiza ocupação e renda de um cliente já cadastrado (<c>POST v1/customer/professional</c>, resposta <c>202</c>).
    /// </summary>
    Task UpsertProfessionalDataAsync(string cpf, ProfessionalData data, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
