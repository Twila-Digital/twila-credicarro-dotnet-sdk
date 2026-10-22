using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Credicarro.Errors;
using Credicarro.Internal;
using Credicarro.Internal.Generated.Webhooks;
using Credicarro.Internal.Mapping;
using Credicarro.Serialization;
using Credicarro.Webhooks.Models;

namespace Credicarro.Webhooks;

/// <summary>
/// Leitura e verificação das notificações de webhook de proposta.
/// <para>
/// Receba o <b>corpo bruto</b> da requisição, antes de qualquer desserialização, e o header
/// <c>X-CrediCarro-Signature</c> (<c>t={timestamp},v1={assinatura}</c>). A assinatura é o HMAC-SHA256, em hexadecimal
/// minúsculo, de <c>{t}.{corpo bruto}</c> com o <c>segredo_assinatura</c> (texto UTF-8, sem decodificar) como chave.
/// </para>
/// </summary>
public static class CredicarroWebhookEvent
{
    /// <summary>Tolerância padrão de replay: 5 minutos, para mais e para menos.</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    private const int MaxTimestampDigits = 10;

    /// <summary>
    /// Verifica a assinatura e a janela de tolerância e devolve o evento. O header de assinatura e o segredo são obrigatórios.
    /// </summary>
    /// <exception cref="CredicarroWebhookSignatureException">Assinatura inválida, cabeçalho ausente ou malformado (inclusive <c>t</c>/<c>v1</c> repetidos), segredo vazio, timestamp fora da tolerância, corpo ilegível ou sem <c>id_proposta</c> válido.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> negativa.</exception>
    public static ProposalWebhookEvent Parse(string rawBody, string signatureHeader, string signingSecret, TimeSpan? tolerance = null) =>
        Parse(rawBody, signatureHeader, signingSecret, tolerance, DateTimeOffset.UtcNow);

    /// <summary>
    /// Lê o evento <b>sem verificar a assinatura</b>. Use só em testes: em produção, valide sempre com
    /// <see cref="Parse(string, string, string, TimeSpan?)"/>.
    /// </summary>
    /// <exception cref="CredicarroWebhookSignatureException">Corpo vazio, que não é um JSON válido ou sem <c>id_proposta</c> UUID válido.</exception>
    public static ProposalWebhookEvent ParseUnverified(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            throw new CredicarroWebhookSignatureException("O corpo do webhook está vazio.");

        ProposalWebhookEventWire? wire;

        try
        {
            wire = JsonSerializer.Deserialize<ProposalWebhookEventWire>(rawBody, CredicarroJsonOptions.Default);
        }
        catch (JsonException)
        {
            throw new CredicarroWebhookSignatureException("O corpo do webhook não é um JSON válido.");
        }

        if (wire is null || wire.IdProposta is null || wire.IdProposta == Guid.Empty)
            throw new CredicarroWebhookSignatureException("O corpo do webhook não traz um id_proposta (UUID) válido.");

        return WebhookMapper.ToPublic(wire);
    }

    /// <summary>
    /// Confere a assinatura e a tolerância, sem lançar exceção por assinatura inválida. Segredo vazio, cabeçalho malformado
    /// e <c>t</c> fora da janela resultam em <see langword="false"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> negativa.</exception>
    public static bool VerifySignature(string rawBody, string signatureHeader, string signingSecret, TimeSpan? tolerance = null) =>
        VerifySignature(rawBody, signatureHeader, signingSecret, tolerance, DateTimeOffset.UtcNow);

    /// <summary>
    /// Compara o header <c>Authorization</c> recebido com o esperado (o configurado no cadastro do webhook:
    /// <c>Basic {credencial}</c> ou <c>Bearer {credencial}</c>) pelo SHA-256 dos dois valores, em tempo constante.
    /// Valores ausentes ou vazios nunca conferem; um esperado vazio nunca é aceito.
    /// </summary>
    public static bool VerifyAuthorization(string? receivedHeader, string? expectedHeader)
    {
        if (string.IsNullOrEmpty(receivedHeader) || string.IsNullOrEmpty(expectedHeader))
            return false;

        using var sha = SHA256.Create();
        var received = sha.ComputeHash(Encoding.UTF8.GetBytes(receivedHeader!));
        var expected = sha.ComputeHash(Encoding.UTF8.GetBytes(expectedHeader!));

        return CryptoUtility.FixedTimeEquals(received, expected);
    }

    internal static ProposalWebhookEvent Parse(string rawBody, string signatureHeader, string signingSecret, TimeSpan? tolerance, DateTimeOffset now)
    {
        if (rawBody is null)
            throw new ArgumentNullException(nameof(rawBody));

        var failure = Validate(rawBody, signatureHeader, signingSecret, RequireTolerance(tolerance), now);

        if (failure is not null)
            throw new CredicarroWebhookSignatureException(failure);

        return ParseUnverified(rawBody);
    }

    internal static bool VerifySignature(string rawBody, string signatureHeader, string signingSecret, TimeSpan? tolerance, DateTimeOffset now)
    {
        var effective = RequireTolerance(tolerance);

        return rawBody is not null && Validate(rawBody, signatureHeader, signingSecret, effective, now) is null;
    }

    internal static string ComputeSignature(string signingSecret, string timestamp, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + rawBody));
        return CryptoUtility.ToHexStringLower(hash);
    }

    private static TimeSpan RequireTolerance(TimeSpan? tolerance)
    {
        var value = tolerance ?? DefaultTolerance;

        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tolerance), "A tolerância precisa ser maior ou igual a zero.");

        return value;
    }

    /// <summary>
    /// Devolve o motivo da falha, ou <see langword="null"/> quando a assinatura e o timestamp são válidos.
    /// </summary>
    private static string? Validate(string rawBody, string? signatureHeader, string? signingSecret, TimeSpan tolerance, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(signingSecret))
            return "O segredo de assinatura do webhook está vazio.";

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return "O cabeçalho de assinatura do webhook está ausente.";

        if (!TryParseHeader(signatureHeader!, out var timestamp, out var signature))
            return "O cabeçalho de assinatura do webhook está malformado.";

        var expected = Encoding.UTF8.GetBytes(ComputeSignature(signingSecret!, timestamp, rawBody));

        if (!CryptoUtility.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(signature)))
            return "A assinatura do webhook não confere.";

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, CultureInfo.InvariantCulture));

        if ((now - eventTime).Duration() > tolerance)
            return "O timestamp do webhook está fora da janela de tolerância: possível replay.";

        return null;
    }

    /// <summary>
    /// Lê <c>t</c> (1 a 10 dígitos) e <c>v1</c>. Repetir <c>t</c> ou <c>v1</c> torna o cabeçalho inválido.
    /// </summary>
    private static bool TryParseHeader(string header, out string timestamp, out string signature)
    {
        timestamp = string.Empty;
        signature = string.Empty;
        var hasTimestamp = false;
        var hasSignature = false;

        foreach (var part in header.Split(','))
        {
            var separator = part.IndexOf('=');

            if (separator <= 0)
                continue;

            var key = part.Substring(0, separator).Trim();
            var value = part.Substring(separator + 1).Trim();

            if (key == "t")
            {
                if (hasTimestamp || !IsValidTimestamp(value))
                    return false;

                timestamp = value;
                hasTimestamp = true;
            }
            else if (key == "v1")
            {
                if (hasSignature || value.Length == 0)
                    return false;

                signature = value.ToLowerInvariant();
                hasSignature = true;
            }
        }

        return hasTimestamp && hasSignature;
    }

    private static bool IsValidTimestamp(string value)
    {
        if (value.Length is < 1 or > MaxTimestampDigits)
            return false;

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
                return false;
        }

        return true;
    }
}
