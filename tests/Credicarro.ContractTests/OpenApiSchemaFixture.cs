using Xunit;

namespace Credicarro.ContractTests;

/// <summary>
/// Baixa o <c>openapi.yaml</c> público da documentação. O swagger de staging exige autenticação (<c>401</c>), por isso
/// o contrato vem da documentação. Use <c>CREDICARRO_CONTRACT_TESTS_OPENAPI_URL</c> para apontar outra URL ou um arquivo local.
/// </summary>
public sealed class OpenApiSchemaFixture : IAsyncLifetime
{
    private const string DefaultOpenApiUrl = "https://documentacao.credicarro.com.br/openapi.yaml";

    public OpenApiDocument Document { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var location = Environment.GetEnvironmentVariable("CREDICARRO_CONTRACT_TESTS_OPENAPI_URL");

        if (string.IsNullOrWhiteSpace(location))
            location = DefaultOpenApiUrl;

        string yaml;

        if (File.Exists(location))
        {
            yaml = await File.ReadAllTextAsync(location);
        }
        else
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            yaml = await httpClient.GetStringAsync(location);
        }

        Document = OpenApiDocument.Parse(yaml);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(OpenApiCollection))]
public sealed class OpenApiCollection : ICollectionFixture<OpenApiSchemaFixture>;
