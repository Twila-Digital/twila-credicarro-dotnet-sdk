namespace Credicarro.Serialization;

/// <summary>
/// Marca o membro de fallback de um enum para valores inteiros não reconhecidos pelo
/// <see cref="UnknownEnumJsonConverterFactory"/>. Exatamente um membro por enum.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class UnknownValueAttribute : Attribute;
