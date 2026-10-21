namespace Credicarro.Internal;

internal static class CryptoUtility
{
    private const string HexAlphabet = "0123456789abcdef";

    public static string ToHexStringLower(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];

        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = HexAlphabet[bytes[i] >> 4];
            chars[i * 2 + 1] = HexAlphabet[bytes[i] & 0xF];
        }

        return new string(chars);
    }

    public static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;

        var result = 0;
        for (var i = 0; i < left.Length; i++)
            result |= left[i] ^ right[i];

        return result == 0;
    }
}
