using Credicarro.Customers.Models;
using Credicarro.Internal.Generated.Customer;

namespace Credicarro.Internal.Mapping;

internal static class CustomerMapper
{
    public static CreateCustomerRequestWire ToWire(CreateCustomerRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new CreateCustomerRequestWire(
            request.Cpf,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.DateOfBirth);
    }

    public static UpsertCustomerRequestWire ToWire(UpsertCustomerRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return new UpsertCustomerRequestWire(
            request.Cpf,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.DateOfBirth,
            ToWire(request.Address),
            ToWire(request.ProfessionalData));
    }

    public static UpsertAddressRequestWire ToWire(string cpf, CustomerAddress address) =>
        new(cpf, ToWire(address));

    public static UpsertProfessionalRequestWire ToWire(string cpf, ProfessionalData data)
    {
        var wire = ToWire(data);
        return new UpsertProfessionalRequestWire(cpf, wire.TipoOcupacao, wire.Renda);
    }

    public static AddressWire ToWire(CustomerAddress address)
    {
        if (address is null)
            throw new ArgumentNullException(nameof(address));

        return new AddressWire(
            address.Street,
            address.Number,
            address.Complement,
            address.Neighborhood,
            address.City,
            address.State,
            address.PostalCode);
    }

    public static ProfessionalDataWire ToWire(ProfessionalData data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        if (!Enum.IsDefined(typeof(OccupationType), data.Occupation))
            throw new ArgumentException("O tipo de ocupação informado não é válido.", nameof(data));

        return new ProfessionalDataWire((int)data.Occupation, data.MonthlyIncome);
    }
}
