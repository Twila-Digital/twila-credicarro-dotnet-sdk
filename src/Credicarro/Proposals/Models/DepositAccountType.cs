namespace Credicarro.Proposals.Models;

/// <summary>
/// Tipo da conta de recebimento (<c>tipoConta</c>).
/// </summary>
public enum DepositAccountType
{
    /// <summary><c>CORRENTE</c></summary>
    Checking = 1,

    /// <summary><c>POUPANCA</c></summary>
    Savings = 2,

    /// <summary><c>PAGAMENTO</c></summary>
    Payment = 3
}
