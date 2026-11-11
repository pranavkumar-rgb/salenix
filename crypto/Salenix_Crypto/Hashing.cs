using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Salenix.Crypto;

/// <summary>Tokens, SHA-256, HMAC-SHA256 and constant-time compare. All text is UTF-8, as in the Node source.</summary>
internal static class Hashing
{
    public const int MinTokenBytes = 16;
    public const int MaxTokenBytes = 64;

    public static string NewToken(int bytes)
    {
        if (bytes < MinTokenBytes || bytes > MaxTokenBytes)
            throw new SalenixCryptoException($"CRYPTO_ERROR: Bytes must be between {MinTokenBytes} and {MaxTokenBytes}.");
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));
    }

    public static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? "")));

    public static string Sha256Base64Url(string value) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? "")));

    public static string HmacSha256Hex(string key, string message) =>
        Convert.ToHexStringLower(HmacSha256(key, message));

    public static byte[] HmacSha256(string key, string message)
    {
        if (string.IsNullOrEmpty(key))
            throw new SalenixCryptoException("CRYPTO_ERROR: Key must not be empty.");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message ?? ""));
    }

    /// <summary>Same as the source safeEqual: different byte length is False at once, else fixed-time compare.</summary>
    public static bool ConstantTimeEquals(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a ?? "");
        var y = Encoding.UTF8.GetBytes(b ?? "");
        return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
    }
}
