using Microsoft.Extensions.DependencyInjection;
using Credicarro;
using Credicarro.Configuration;
using Credicarro.Customers.Models;
using Credicarro.DependencyInjection;
using Credicarro.Errors;
using Credicarro.Proposals.Models;
using Credicarro.Simulations.Models;

var clientId = Environment.GetEnvironmentVariable("CREDICARRO_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("CREDICARRO_CLIENT_SECRET");
var storeCnpj = Environment.GetEnvironmentVariable("CREDICARRO_STORE_CNPJ");

if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(storeCnpj))
{
    Console.WriteLine("Defina CREDICARRO_CLIENT_ID, CREDICARRO_CLIENT_SECRET e CREDICARRO_STORE_CNPJ (credenciais e loja de staging) antes de rodar este sample.");
    return 1;
}

var services = new ServiceCollection();

services.AddCredicarro(options =>
{
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.Environment = CredicarroEnvironment.Staging;
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ICredicarroClient>();

try
{
    Console.WriteLine("Cadastrando o cliente...");

    await client.Customers.UpsertAsync(new UpsertCustomerRequest(
        Cpf: "12345678909",
        FirstName: "Maria",
        LastName: "Souza",
        Email: "maria.souza@exemplo.com.br",
        PhoneNumber: "+55 (11) 99876-5432",
        DateOfBirth: new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
        Address: new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000", Number: "1578"),
        ProfessionalData: new ProfessionalData(OccupationType.Employee, 6500.00m)));

    Console.WriteLine("Consultando a entrada mínima por prazo...");

    var range = await client.Simulations.GetFinancingRangeAsync("ABC1D23", storeCnpj);

    foreach (var option in range)
        Console.WriteLine($"  {option.Term}x: entrada mínima de R$ {option.MinimumDownPayment:0.00}");

    Console.WriteLine("Simulando o financiamento...");

    var simulations = await client.Simulations.CreateFinancingAsync(new CreateFinancingSimulationRequest(
        Cpf: "12345678909",
        LicensePlate: "ABC1D23",
        VehicleAmount: 45000m,
        DownPayment: range.Count > 0 ? range[0].MinimumDownPayment : 9000m,
        State: "SP",
        IsZeroKm: false,
        StoreCnpj: storeCnpj));

    foreach (var simulation in simulations)
        Console.WriteLine($"  {simulation.Term}x de R$ {simulation.InstallmentAmount:0.00} (CET a.a. {simulation.AnnualCet:0.00}%)");

    var proposalId = await client.Proposals.CreateAsync(new CreateProposalRequest("12345678909", simulations[0].SimulationId));
    Console.WriteLine($"Proposta criada: {proposalId}");

    var proposal = await client.Proposals.GetAsync(proposalId);
    Console.WriteLine($"Status: {proposal.StatusName} ({proposal.Status})");

    foreach (var action in proposal.GetPendingActions())
        Console.WriteLine($"  Pendente: {action.Kind} do {action.Participant} (etapa {action.Step})");

    return 0;
}
catch (CredicarroAuthenticationException ex)
{
    Console.WriteLine($"Falha de autenticação, confira ClientId/ClientSecret: {ex.Message}");
    return 1;
}
catch (CredicarroBusinessRuleException ex)
{
    Console.WriteLine($"Regra de negócio não atendida ({ex.ErrorCode}): {ex.Message}");
    return 1;
}
catch (CredicarroApiException ex)
{
    Console.WriteLine($"Erro da API ({(int)ex.StatusCode}, {ex.ErrorCode}, campo: {ex.Field}): {ex.Message}");
    return 1;
}
