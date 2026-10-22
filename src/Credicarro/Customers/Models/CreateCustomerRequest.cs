using Credicarro.Internal;

namespace Credicarro.Customers.Models;

/// <summary>
/// Dados pessoais do cliente (<c>POST v1/customer</c>).
/// </summary>
/// <param name="Cpf">CPF com ou sem pontuação (<c>cpf</c>).</param>
/// <param name="FirstName">Primeiro nome (<c>nome</c>), até 100 caracteres.</param>
/// <param name="LastName">Sobrenome (<c>sobrenome</c>), até 100 caracteres.</param>
/// <param name="Email">E-mail (<c>email</c>).</param>
/// <param name="PhoneNumber">Celular exatamente no formato <c>+55 (DD) 99999-9999</c> (<c>celular</c>).</param>
/// <param name="DateOfBirth">Data de nascimento (<c>dataDeNascimento</c>), ISO-8601 com offset; o cliente precisa ter entre 18 e 75 anos.</param>
public sealed record CreateCustomerRequest(
    string Cpf,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTimeOffset DateOfBirth)
{
    public override string ToString() =>
        $"CreateCustomerRequest {{ Cpf = {Masking.Cpf(Cpf)}, FirstName = {Masking.Hidden}, LastName = {Masking.Hidden}, Email = {Masking.Hidden}, PhoneNumber = {Masking.Hidden}, DateOfBirth = {Masking.Hidden} }}";
}
