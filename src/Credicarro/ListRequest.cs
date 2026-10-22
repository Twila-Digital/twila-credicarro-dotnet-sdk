using Credicarro.Http;

namespace Credicarro;

/// <summary>
/// Parâmetros de paginação das listagens (<c>pagina_inicial</c> e <c>limite</c>).
/// </summary>
/// <param name="Page">Página inicial, a partir de 1.</param>
/// <param name="PageSize">Itens por página, de 1 a 100.</param>
public sealed record ListRequest(int Page = 1, int PageSize = 10)
{
    internal string ToPath(string path)
    {
        if (Page < 1)
            throw new ArgumentOutOfRangeException(nameof(Page), Page, "Page deve ser maior ou igual a 1.");

        if (PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "PageSize deve estar entre 1 e 100.");

        return new QueryStringBuilder()
            .Add("pagina_inicial", Page)
            .Add("limite", PageSize)
            .Build(path);
    }
}
