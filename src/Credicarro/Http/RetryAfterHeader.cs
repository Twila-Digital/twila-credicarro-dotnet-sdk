namespace Credicarro.Http;

/// <summary>
/// Leitura defensiva do <c>Retry-After</c> (segundos ou data HTTP). Valor inválido, zero, negativo ou data no passado é
/// ignorado; valores absurdos são limitados a 24 horas antes de virar duração.
/// </summary>
internal static class RetryAfterHeader
{
    internal static readonly TimeSpan MaxValue = TimeSpan.FromHours(24);

    public static TimeSpan? From(HttpResponseMessage? response) => From(response, DateTimeOffset.UtcNow);

    public static TimeSpan? From(HttpResponseMessage? response, DateTimeOffset now)
    {
        var header = response?.Headers.RetryAfter;

        if (header is null)
            return null;

        TimeSpan? value = null;

        if (header.Delta is { } delta)
            value = delta;
        else if (header.Date is { } date)
            value = date > now.Add(MaxValue) ? MaxValue : date - now;

        if (value is null || value.Value <= TimeSpan.Zero)
            return null;

        return value.Value > MaxValue ? MaxValue : value;
    }
}
