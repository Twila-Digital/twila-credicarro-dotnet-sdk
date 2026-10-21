namespace Credicarro.Http;

/// <summary>
/// Reconhece as rotas da API pelo caminho (sem o prefixo <c>/integration</c>), para decidir idempotência e retry.
/// </summary>
internal static class ApiRoutes
{
    private static readonly string[] AutomaticIdempotencyRoutes =
    [
        "v1/customer",
        "v1/customer/address",
        "v1/customer/professional",
        "v1/proposal/simulation/financing",
        "v1/proposal/simulation/refinancing",
        "v2/proposal/simulation/financing",
        "v2/proposal/simulation/refinancing",
        "v1/proposal",
        "v2/proposal"
    ];

    /// <summary>
    /// Caminho a partir da versão da API (<c>v1/...</c> ou <c>v2/...</c>), sem barras nas pontas e sem query string.
    /// </summary>
    public static string GetApiPath(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is null)
            return string.Empty;

        var rawPath = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString.Split('?')[0];
        var trimmed = rawPath.Trim('/');
        var padded = "/" + trimmed + "/";

        var v1 = padded.IndexOf("/v1/", StringComparison.OrdinalIgnoreCase);
        var v2 = padded.IndexOf("/v2/", StringComparison.OrdinalIgnoreCase);
        var index = v1 < 0 ? v2 : v2 < 0 ? v1 : Math.Min(v1, v2);

        return index < 0 ? trimmed : padded.Substring(index + 1).TrimEnd('/');
    }

    public static bool IsAuthentication(string path) =>
        string.Equals(path, "v1/authentication/accesstoken", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresAutomaticIdempotencyKey(string path) =>
        Array.Exists(AutomaticIdempotencyRoutes, route => string.Equals(path, route, StringComparison.OrdinalIgnoreCase))
        || IsDocumentUpload(path);

    /// <summary><c>POST v2/proposal/{id}/document</c></summary>
    public static bool IsDocumentUpload(string path) => IsProposalAction(path, "document");

    /// <summary><c>POST v2/proposal/{id}/vehicle-detail</c> e <c>POST v2/proposal/{id}/deposit-account</c> (semântica de substituição).</summary>
    public static bool IsReplaceSemantics(string path) =>
        IsProposalAction(path, "vehicle-detail") || IsProposalAction(path, "deposit-account");

    /// <summary><c>PUT/DELETE v1/webhook/{id}</c></summary>
    public static bool IsWebhookResource(string path)
    {
        var segments = path.Split('/');

        return segments.Length == 3
            && string.Equals(segments[0], "v1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "webhook", StringComparison.OrdinalIgnoreCase)
            && segments[2].Length > 0;
    }

    private static bool IsProposalAction(string path, string action)
    {
        var segments = path.Split('/');

        return segments.Length == 4
            && string.Equals(segments[0], "v2", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "proposal", StringComparison.OrdinalIgnoreCase)
            && segments[2].Length > 0
            && string.Equals(segments[3], action, StringComparison.OrdinalIgnoreCase);
    }
}
