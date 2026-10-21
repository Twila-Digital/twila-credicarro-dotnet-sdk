namespace Credicarro;

/// <summary>
/// Página de resultados. Não há auto-paginação: avance a página explicitamente com <see cref="ListRequest"/>.
/// </summary>
public sealed class PagedResult<T>(IReadOnlyList<T> items, int page, int pageSize, long totalCount, bool hasNext, bool hasPrevious)
{
    public IReadOnlyList<T> Items { get; } = items;

    /// <summary>Página atual (<c>numero</c>).</summary>
    public int Page { get; } = page;

    /// <summary>Quantidade de itens por página (<c>tamanho</c>).</summary>
    public int PageSize { get; } = pageSize;

    /// <summary>Total de itens encontrados (<c>total</c>).</summary>
    public long TotalCount { get; } = totalCount;

    public bool HasNext { get; } = hasNext;

    public bool HasPrevious { get; } = hasPrevious;
}
