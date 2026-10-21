using Credicarro.Internal;

namespace Credicarro.Customers.Models;

/// <summary>
/// Endereço do cliente.
/// </summary>
/// <param name="Street">Rua (<c>rua</c>).</param>
/// <param name="Neighborhood">Bairro (<c>bairro</c>).</param>
/// <param name="City">Cidade (<c>cidade</c>).</param>
/// <param name="State">UF (<c>estado</c>), por exemplo <c>SP</c>.</param>
/// <param name="PostalCode">CEP com 8 dígitos, com ou sem hífen (<c>cep</c>).</param>
/// <param name="Number">Número (<c>numero</c>), opcional.</param>
/// <param name="Complement">Complemento (<c>complemento</c>), opcional.</param>
public sealed record CustomerAddress(
    string Street,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    string? Number = null,
    string? Complement = null)
{
    public override string ToString() =>
        $"CustomerAddress {{ Street = {Masking.Hidden}, Neighborhood = {Masking.Hidden}, City = {Masking.Hidden}, State = {Masking.Hidden}, PostalCode = {Masking.Hidden}, Number = {Masking.Hidden}, Complement = {Masking.Hidden} }}";
}
