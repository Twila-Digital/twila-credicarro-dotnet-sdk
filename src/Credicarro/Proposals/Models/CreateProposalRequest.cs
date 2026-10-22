using Credicarro.Internal;

namespace Credicarro.Proposals.Models;

/// <summary>
/// Criação da proposta a partir de uma opção de simulação.
/// </summary>
/// <param name="Cpf">CPF do cliente (<c>cpf</c>).</param>
/// <param name="SimulationId">Identificador da opção escolhida, vindo de <c>id_simulacao</c> (<c>simulacaoId</c>). Cada opção gera uma única proposta.</param>
public sealed record CreateProposalRequest(string Cpf, Guid SimulationId)
{
    public override string ToString() =>
        $"CreateProposalRequest {{ Cpf = {Masking.Cpf(Cpf)}, SimulationId = {SimulationId} }}";
}
