using Credicarro.Customers.Models;
using Credicarro.Http;
using Credicarro.Internal.Generated.Customer;
using Credicarro.Internal.Mapping;

namespace Credicarro.Customers;

internal sealed class CustomersClient(HttpClient httpClient) : ICustomersClient
{
    public async Task<Guid> UpsertAsync(UpsertCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var key = ApiTransport.ValidateIdempotencyKey(idempotencyKey, nameof(idempotencyKey));
        var wireRequest = CustomerMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<CustomerIdResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/customer/unified", wireRequest, key),
                cancellationToken)
            .ConfigureAwait(false);

        return wire.IdCliente;
    }

    public async Task<Guid> CreateAsync(CreateCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var key = ApiTransport.ValidateIdempotencyKey(idempotencyKey, nameof(idempotencyKey));
        var wireRequest = CustomerMapper.ToWire(request);

        var wire = await ApiTransport
            .SendAsync<CustomerIdResponseWire>(
                httpClient,
                ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/customer", wireRequest, key),
                cancellationToken)
            .ConfigureAwait(false);

        return wire.IdCliente;
    }

    public Task UpsertAddressAsync(string cpf, CustomerAddress address, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        RequireCpf(cpf);
        var key = ApiTransport.ValidateIdempotencyKey(idempotencyKey, nameof(idempotencyKey));
        var wireRequest = CustomerMapper.ToWire(cpf, address);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/customer/address", wireRequest, key),
            cancellationToken);
    }

    public Task UpsertProfessionalDataAsync(string cpf, ProfessionalData data, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        RequireCpf(cpf);
        var key = ApiTransport.ValidateIdempotencyKey(idempotencyKey, nameof(idempotencyKey));
        var wireRequest = CustomerMapper.ToWire(cpf, data);

        return ApiTransport.SendAsync(
            httpClient,
            ApiTransport.CreateJsonRequest(HttpMethod.Post, "v1/customer/professional", wireRequest, key),
            cancellationToken);
    }

    private static void RequireCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            throw new ArgumentException("O CPF é obrigatório.", nameof(cpf));
    }
}
