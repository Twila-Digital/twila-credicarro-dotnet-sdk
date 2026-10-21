using Credicarro.Errors;

namespace Credicarro.UnitTests.Errors;

public class ProblemDetailsModelTests
{
    [Fact]
    public void Parse_ErroDeCampo_LeTipoTituloStatusDetalheECampo()
    {
        const string json = """
            {
                "tipo": "Validation.Cpf.Invalid",
                "titulo": "Requisição inválida",
                "status": 400,
                "detalhe": "O CPF informado em 'cpf' é inválido.",
                "campo": "cpf"
            }
            """;

        var result = ProblemDetailsModel.Parse(json);

        Assert.Equal("Validation.Cpf.Invalid", result.Type);
        Assert.Equal("Requisição inválida", result.Title);
        Assert.Equal(400, result.Status);
        Assert.Equal("O CPF informado em 'cpf' é inválido.", result.Detail);
        Assert.Equal("cpf", result.Field);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_ErroDeRegraDeNegocio_SemCampo()
    {
        const string json = """
            {
                "tipo": "Store.Integration.Intermediation",
                "titulo": "Regra de negócio não atendida",
                "status": 422,
                "detalhe": "A loja possui contrato de intermediação e não é elegível para a integração."
            }
            """;

        var result = ProblemDetailsModel.Parse(json);

        Assert.Equal("Store.Integration.Intermediation", result.Type);
        Assert.Null(result.Field);
    }

    [Fact]
    public void Parse_ComListaDeErros_LeCodigoMensagemECampo()
    {
        const string json = """
            {
                "tipo": "Validation.InvalidFormat",
                "titulo": "Requisição inválida",
                "status": 400,
                "erros": [
                    { "codigo": "Validation.InvalidFormat", "mensagem": "O valor informado para 'valorVeiculo' não é um número válido.", "campo": "valorVeiculo" },
                    { "codigo": "Validation.InvalidFormat", "mensagem": "Corpo malformado." }
                ]
            }
            """;

        var result = ProblemDetailsModel.Parse(json);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("Validation.InvalidFormat", result.Errors[0].Code);
        Assert.Equal("valorVeiculo", result.Errors[0].Field);
        Assert.Null(result.Errors[1].Field);
    }

    [Fact]
    public void Parse_ComFormatoPadraoTypeTitleDetail_UsaOFallback()
    {
        const string json = """
            {
                "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                "title": "An error occurred while processing your request.",
                "status": 500,
                "detail": "Erro inesperado."
            }
            """;

        var result = ProblemDetailsModel.Parse(json);

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.6.1", result.Type);
        Assert.Equal("An error occurred while processing your request.", result.Title);
        Assert.Equal("Erro inesperado.", result.Detail);
        Assert.Equal(500, result.Status);
    }

    [Fact]
    public void Parse_QuandoOsCamposEmPortuguesExistem_TemPrioridadeSobreOsPadrao()
    {
        const string json = """{ "tipo": "Proposal.Access.NotFound", "type": "outro", "titulo": "Não encontrado", "title": "Not found" }""";

        var result = ProblemDetailsModel.Parse(json);

        Assert.Equal("Proposal.Access.NotFound", result.Type);
        Assert.Equal("Não encontrado", result.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("isto não é json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"texto\"")]
    public void Parse_ComCorpoVazioInvalidoOuQueNaoEObjeto_DevolveModeloVazio(string? body)
    {
        var result = ProblemDetailsModel.Parse(body);

        Assert.Null(result.Type);
        Assert.Null(result.Title);
        Assert.Null(result.Detail);
        Assert.Null(result.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_ComCamposDeTipoInesperado_NaoLanca()
    {
        const string json = """{ "tipo": 123, "status": "quatrocentos", "erros": { "campo": ["a"] }, "campo": { "x": 1 } }""";

        var result = ProblemDetailsModel.Parse(json);

        Assert.Null(result.Type);
        Assert.Null(result.Status);
        Assert.Null(result.Field);
        Assert.Empty(result.Errors);
    }
}
