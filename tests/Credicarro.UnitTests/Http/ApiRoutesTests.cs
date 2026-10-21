using Credicarro.Http;

namespace Credicarro.UnitTests.Http;

public class ApiRoutesTests
{
    private const string ProposalId = "bea8d060-f93d-48eb-b6c4-45f64483cdd9";

    private static string PathOf(string url) => ApiRoutes.GetApiPath(new HttpRequestMessage(HttpMethod.Get, url));

    [Theory]
    [InlineData("https://api.credicarro.com.br/integration/v1/customer", "v1/customer")]
    [InlineData("https://api.credicarro.com.br/integration/v2/proposal/" + ProposalId + "/document", "v2/proposal/" + ProposalId + "/document")]
    [InlineData("https://api.credicarro.com.br/integration/v1/proposal?pagina_inicial=1&limite=10", "v1/proposal")]
    [InlineData("https://mock.local/v1/webhook/", "v1/webhook")]
    [InlineData("https://mock.local/outro/prefixo/v2/proposal", "v2/proposal")]
    public void GetApiPath_RemoveOPrefixoEAQueryString(string url, string expected)
    {
        Assert.Equal(expected, PathOf(url));
    }

    [Fact]
    public void GetApiPath_ComUriRelativa_FuncionaIgual()
    {
        Assert.Equal("v2/proposal", PathOf("v2/proposal?x=1"));
    }

    [Theory]
    [InlineData("v2/proposal/" + ProposalId + "/document", true)]
    [InlineData("v2/proposal/" + ProposalId + "/vehicle-detail", false)]
    [InlineData("v2/proposal//document", false)]
    [InlineData("v1/proposal/" + ProposalId + "/document", false)]
    [InlineData("v2/proposal", false)]
    public void IsDocumentUpload_ReconheceSoARotaDeDocumento(string path, bool expected)
    {
        Assert.Equal(expected, ApiRoutes.IsDocumentUpload(path));
    }

    [Theory]
    [InlineData("v2/proposal/" + ProposalId + "/vehicle-detail", true)]
    [InlineData("v2/proposal/" + ProposalId + "/deposit-account", true)]
    [InlineData("v2/proposal/" + ProposalId + "/document", false)]
    public void IsReplaceSemantics_ReconheceVeiculoEContaDeRecebimento(string path, bool expected)
    {
        Assert.Equal(expected, ApiRoutes.IsReplaceSemantics(path));
    }

    [Theory]
    [InlineData("v1/webhook/3fa85f64-5717-4562-b3fc-2c963f66afa6", true)]
    [InlineData("v1/webhook", false)]
    [InlineData("v1/webhook/a/b", false)]
    [InlineData("v1/customer/x", false)]
    public void IsWebhookResource_ReconheceSoOWebhookPorId(string path, bool expected)
    {
        Assert.Equal(expected, ApiRoutes.IsWebhookResource(path));
    }

    [Fact]
    public void RequiresAutomaticIdempotencyKey_ExcluiUnifiedEWebhook()
    {
        Assert.False(ApiRoutes.RequiresAutomaticIdempotencyKey("v1/customer/unified"));
        Assert.False(ApiRoutes.RequiresAutomaticIdempotencyKey("v1/webhook"));
        Assert.True(ApiRoutes.RequiresAutomaticIdempotencyKey("v1/customer"));
    }
}
