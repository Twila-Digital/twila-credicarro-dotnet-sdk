using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Credicarro;
using Credicarro.Configuration;
using Credicarro.DependencyInjection;
using Credicarro.Errors;

namespace Credicarro.Sample.NetFramework
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
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

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<ICredicarroClient>();

                try
                {
                    Console.WriteLine("Consultando a entrada mínima por prazo...");

                    var range = await client.Simulations.GetFinancingRangeAsync("ABC1D23", storeCnpj);

                    foreach (var option in range)
                        Console.WriteLine(string.Format("  {0}x: entrada mínima de R$ {1:0.00}", option.Term, option.MinimumDownPayment));

                    return 0;
                }
                catch (CredicarroAuthenticationException ex)
                {
                    Console.WriteLine("Falha de autenticação, confira ClientId/ClientSecret: " + ex.Message);
                    return 1;
                }
                catch (CredicarroApiException ex)
                {
                    Console.WriteLine(string.Format("Erro da API ({0}, {1}): {2}", (int)ex.StatusCode, ex.ErrorCode, ex.Message));
                    return 1;
                }
            }
        }
    }
}
