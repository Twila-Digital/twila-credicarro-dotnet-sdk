using Credicarro.Internal;

namespace Credicarro.Customers.Models;

/// <summary>
/// Cadastro completo do cliente (<c>POST v1/customer/unified</c>): dados pessoais, endereço e dados profissionais.
/// </summary>
/// <param name="Cpf">CPF com ou sem pontuação (<c>cpf</c>).</param>
/// <param name="FirstName">Primeiro nome (<c>nome</c>).</param>
/// <param name="LastName">Sobrenome (<c>sobrenome</c>).</param>
/// <param name="Email">E-mail (<c>email</c>).</param>
/// <param name="PhoneNumber">Celular no formato <c>+55 (DD) 99999-9999</c> (<c>celular</c>).</param>
/// <param name="DateOfBirth">Data de nascimento (<c>dataDeNascimento</c>).</param>
/// <param name="Address">Endereço (<c>endereco</c>).</param>
/// <param name="ProfessionalData">Ocupação e renda (<c>dadosProfissionais</c>).</param>
public sealed record UpsertCustomerRequest(
    string Cpf,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTimeOffset DateOfBirth,
    CustomerAddress Address,
    ProfessionalData ProfessionalData)
{
    public override string ToString() =>
        $"UpsertCustomerRequest {{ Cpf = {Masking.Cpf(Cpf)}, FirstName = {Masking.Hidden}, LastName = {Masking.Hidden}, Email = {Masking.Hidden}, PhoneNumber = {Masking.Hidden}, DateOfBirth = {Masking.Hidden}, Address = {Address}, ProfessionalData = {ProfessionalData} }}";
}
