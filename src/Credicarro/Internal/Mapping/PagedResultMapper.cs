using Credicarro.Internal.Generated;

namespace Credicarro.Internal.Mapping;

internal static class PagedResultMapper
{
    public static PagedResult<TPublic> ToPublic<TWire, TPublic>(PagedResultWire<TWire> wire, Func<TWire, TPublic> map)
    {
        var items = new List<TPublic>();

        if (wire.Items is not null)
        {
            foreach (var item in wire.Items)
                items.Add(map(item));
        }

        var page = wire.Page;

        return page is null
            ? new PagedResult<TPublic>(items, 0, items.Count, items.Count, false, false)
            : new PagedResult<TPublic>(items, page.Number, page.Size, page.Total, page.HasNext, page.HasPrevious);
    }
}
