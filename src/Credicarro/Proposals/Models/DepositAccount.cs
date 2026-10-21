using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Conta de recebimento de uma proposta de refinanciamento (<c>POST v2/proposal/{id}/deposit-account</c>).
/// O titular deve ser o cliente da proposta. Enviar de novo substitui a conta anterior.
/// </summary>
/// <param name="HolderCpf">CPF do titular (<c>cpfTitular</c>).</param>
/// <param name="BankCode">Código do banco (COMPE) com 3 dígitos (<c>codigoBanco</c>).</param>
/// <param name="Branch">Agência, até 4 dígitos, sem o dígito (<c>agencia</c>).</param>
/// <param name="BranchDigit">Dígito da agência, número ou X (<c>digitoAgencia</c>).</param>
/// <param name="AccountNumber">Conta, de 4 a 13 dígitos, sem o dígito (<c>conta</c>).</param>
/// <param name="AccountDigit">Dígito da conta, número ou X (<c>digitoConta</c>).</param>
/// <param name="AccountType">Tipo da conta (<c>tipoConta</c>).</param>
public sealed record DepositAccount(
    string HolderCpf,
    string BankCode,
    string Branch,
    string BranchDigit,
    string AccountNumber,
    string AccountDigit,
    DepositAccountType AccountType)
{
    public override string ToString() =>
        $"DepositAccount {{ HolderCpf = {Masking.Cpf(HolderCpf)}, BankCode = {Masking.Hidden}, Branch = {Masking.Hidden}, BranchDigit = {Masking.Hidden}, AccountNumber = {Masking.Hidden}, AccountDigit = {Masking.Hidden}, AccountType = {AccountType} }}";
}
