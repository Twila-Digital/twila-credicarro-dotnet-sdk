using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Credicarro.Authentication;
using Credicarro.Configuration;
using Credicarro.Customers;
using Credicarro.Errors;
using Credicarro.Http;
using Credicarro.Idempotency;
using Credicarro.Proposals;
using Credicarro.Resilience;
using Credicarro.Simulations;
using Credicarro.V1;
using Credicarro.Webhooks;

namespace Credicarro.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string ApiPipelineKey = "credicarro.api";
    private const string AuthPipelineKey = "credicarro.auth";

    /// <summary>
    /// Registra o <see cref="ICredicarroClient"/> (singleton) com autenticação, token em cache, retry, circuit breaker e
    /// idempotência. Devolve o <see cref="IHttpClientBuilder"/> do cliente de API para configurações extras
    /// (por exemplo, <c>ConfigurePrimaryHttpMessageHandler</c>).
    /// </summary>
    /// <exception cref="CredicarroConfigurationException">
    /// Na primeira resolução do client, se as opções forem inválidas. A mensagem nunca traz valores, só o nome do campo.
    /// </exception>
    public static IHttpClientBuilder AddCredicarro(this IServiceCollection services, Action<CredicarroOptions> configureOptions)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        if (configureOptions is null)
            throw new ArgumentNullException(nameof(configureOptions));

        services.AddOptions<CredicarroOptions>().Configure(configureOptions).ValidateDataAnnotations();
        services.AddSingleton<IValidateOptions<CredicarroOptions>, CredicarroOptionsValidator>();

        services.AddSingleton(ResolveOptions);

        services.AddKeyedSingleton(ApiPipelineKey, (sp, _) => ResiliencePipelineFactory.Create(sp.GetRequiredService<CredicarroOptions>().Resilience));
        services.AddKeyedSingleton(AuthPipelineKey, (sp, _) => ResiliencePipelineFactory.Create(sp.GetRequiredService<CredicarroOptions>().Resilience));

        services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
        services.AddTransient<AuthenticationDelegatingHandler>();
        services.AddTransient<IdempotencyKeyDelegatingHandler>();

        services.AddHttpClient<ITokenApiClient, TokenApiClient>(HttpClientNames.Auth, (sp, client) =>
            {
                client.BaseAddress = sp.GetRequiredService<CredicarroOptions>().ResolveBaseUrl();
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
            .AddHttpMessageHandler(sp => new ResilienceDelegatingHandler(sp.GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>(AuthPipelineKey)));

        var apiClientBuilder = services.AddHttpClient(HttpClientNames.Api, (sp, client) =>
            {
                client.BaseAddress = sp.GetRequiredService<CredicarroOptions>().ResolveBaseUrl();
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
            .AddHttpMessageHandler<IdempotencyKeyDelegatingHandler>()
            .AddHttpMessageHandler(sp => new ResilienceDelegatingHandler(sp.GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>(ApiPipelineKey)))
            .AddHttpMessageHandler<AuthenticationDelegatingHandler>();

        services.AddHttpClient<ICustomersClient, CustomersClient>(HttpClientNames.Api);
        services.AddHttpClient<ISimulationsClient, SimulationsClient>(HttpClientNames.Api);
        services.AddHttpClient<IProposalsClient, ProposalsClient>(HttpClientNames.Api);
        services.AddHttpClient<IWebhooksClient, WebhooksClient>(HttpClientNames.Api);
        services.AddHttpClient<IV1SimulationsClient, V1SimulationsClient>(HttpClientNames.Api);
        services.AddHttpClient<IV1ProposalsClient, V1ProposalsClient>(HttpClientNames.Api);
        services.AddSingleton<IV1Client, V1Client>();
        services.AddSingleton<ICredicarroClient, CredicarroClient>();

        return apiClientBuilder;
    }

    /// <summary>
    /// Handler primário sem redirects: um 3xx não pode levar o Bearer token (nem o corpo) para outro host; ele vira
    /// <c>CredicarroApiException</c>.
    /// </summary>
    private static HttpMessageHandler CreatePrimaryHandler() => new HttpClientHandler { AllowAutoRedirect = false };

    private static CredicarroOptions ResolveOptions(IServiceProvider serviceProvider)
    {
        try
        {
            return serviceProvider.GetRequiredService<IOptions<CredicarroOptions>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            throw new CredicarroConfigurationException(
                "Configuração do Credicarro inválida: " + string.Join(" ", ex.Failures),
                ex);
        }
    }
}
