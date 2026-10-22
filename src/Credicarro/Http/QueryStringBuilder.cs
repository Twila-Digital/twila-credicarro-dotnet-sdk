using System.Globalization;
using System.Text;

namespace Credicarro.Http;

internal sealed class QueryStringBuilder
{
    private readonly List<string> _parameters = [];

    public QueryStringBuilder Add(string name, string? value)
    {
        if (value is not null)
            _parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");

        return this;
    }

    public QueryStringBuilder Add(string name, int? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryStringBuilder Add(string name, long? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryStringBuilder Add(string name, decimal? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryStringBuilder Add(string name, DateTimeOffset? value) =>
        Add(name, value?.ToString("o", CultureInfo.InvariantCulture));

    public QueryStringBuilder Add(string name, Guid? value) =>
        Add(name, value?.ToString());

    public string Build(string path)
    {
        if (_parameters.Count == 0)
            return path;

        var builder = new StringBuilder(path).Append('?');
        builder.Append(string.Join("&", _parameters));
        return builder.ToString();
    }
}
