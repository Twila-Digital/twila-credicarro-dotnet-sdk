using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credicarro.Serialization;

/// <summary>
/// Converte enums de/para inteiro. Um valor não definido no enum é mapeado para o membro marcado com
/// <see cref="UnknownValueAttribute"/> em vez de lançar <see cref="JsonException"/>.
/// </summary>
public sealed class UnknownEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsEnum && FindUnknownMember(typeToConvert) is not null;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(UnknownEnumJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    internal static FieldInfo? FindUnknownMember(Type enumType) =>
        enumType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(field => field.GetCustomAttribute<UnknownValueAttribute>() is not null);

    private sealed class UnknownEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private readonly TEnum _unknownValue;

#pragma warning disable S1144
        public UnknownEnumJsonConverter()
#pragma warning restore S1144
        {
            var field = FindUnknownMember(typeof(TEnum))
                ?? throw new InvalidOperationException(
                    $"O enum {typeof(TEnum).Name} não tem um membro marcado com [UnknownValue].");

            _unknownValue = (TEnum)field.GetValue(null)!;
        }

#pragma warning disable S1172
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
#pragma warning restore S1172
        {
            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var rawValue))
                return _unknownValue;

            var candidate = (TEnum)Enum.ToObject(typeof(TEnum), rawValue);
            return Enum.IsDefined(typeof(TEnum), candidate) ? candidate : _unknownValue;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt32(value));
    }
}
