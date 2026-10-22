using System.Globalization;

namespace Credicarro.Serialization;

internal static class EnumMapping
{
    public static TEnum FromWireValue<TEnum>(int value) where TEnum : struct, Enum
    {
        var candidate = (TEnum)Enum.ToObject(typeof(TEnum), value);

        if (Enum.IsDefined(typeof(TEnum), candidate))
            return candidate;

        return Unknown<TEnum>();
    }

    /// <summary>
    /// Converte o texto devolvido pela API (ex.: <c>"Proposal"</c>, <c>"Basic"</c>) para o enum.
    /// Aceita também o número em texto. Qualquer outro valor vira o membro <c>[UnknownValue]</c>.
    /// </summary>
    public static TEnum FromWireName<TEnum>(string? name) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(name))
            return Unknown<TEnum>();

        var text = name!.Trim();

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return FromWireValue<TEnum>(number);

        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(TEnum), parsed))
            return parsed;

        return Unknown<TEnum>();
    }

    public static TEnum Unknown<TEnum>() where TEnum : struct, Enum
    {
        var unknownField = UnknownEnumJsonConverterFactory.FindUnknownMember(typeof(TEnum))
            ?? throw new InvalidOperationException($"O enum {typeof(TEnum).Name} não tem um membro marcado com [UnknownValue].");

        return (TEnum)unknownField.GetValue(null)!;
    }

    public static bool IsKnown<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Enum.IsDefined(typeof(TEnum), value) && !EqualityComparer<TEnum>.Default.Equals(value, Unknown<TEnum>());
}
