using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Salenix.Crypto;

/// <summary>
/// HS256 verification as in source/src/lib/askmira/core.ts verify(): alg must be HS256, HMAC over
/// "header.payload" with the UTF-8 secret, iss equal, aud (string or array) contains the audience,
/// exp required with leeway, iat (if present) not in the future beyond leeway. No signing.
/// </summary>
internal static class JwtHs256
{
    public static JwtVerifyResult Verify(string token, string secret, string issuer, string audience, int leewaySeconds, long nowUnix)
    {
        if (string.IsNullOrEmpty(secret))
            throw new SalenixCryptoException("CRYPTO_ERROR: Secret must not be empty.");

        var parts = (token ?? "").Split('.');
        if (parts.Length != 3) return Fail("malformed");

        using var header = ParseObject(parts[0]);
        if (header is null) return Fail("malformed");
        if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.ValueKind != JsonValueKind.String || alg.GetString() != "HS256")
            return Fail("bad_alg");

        var given = DecodeBase64Url(parts[2]);
        if (given is null) return Fail("malformed");
        var expected = Hashing.HmacSha256(secret, $"{parts[0]}.{parts[1]}");
        if (given.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(given, expected))
            return Fail("bad_signature");

        using var payload = ParseObject(parts[1]);
        if (payload is null) return Fail("malformed");
        var claims = payload.RootElement;

        if (!claims.TryGetProperty("iss", out var iss) || iss.ValueKind != JsonValueKind.String || iss.GetString() != issuer)
            return Fail("bad_issuer");
        if (!HasAudience(claims, audience)) return Fail("bad_audience");
        if (!claims.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number || exp.GetDouble() + leewaySeconds < nowUnix)
            return Fail("expired");
        if (claims.TryGetProperty("iat", out var iat) && iat.ValueKind == JsonValueKind.Number && iat.GetDouble() - leewaySeconds > nowUnix)
            return Fail("not_yet_valid");

        return new JwtVerifyResult { IsValid = true, Reason = "", ClaimsJson = claims.GetRawText() };
    }

    /// <summary>Payload JSON without any check (only to choose the secret); "" when malformed.</summary>
    public static string Peek(string token)
    {
        var parts = (token ?? "").Split('.');
        if (parts.Length != 3) return "";
        using var payload = ParseObject(parts[1]);
        return payload?.RootElement.GetRawText() ?? "";
    }

    private static bool HasAudience(JsonElement claims, string audience)
    {
        if (!claims.TryGetProperty("aud", out var aud)) return false;
        return aud.ValueKind switch
        {
            JsonValueKind.String => aud.GetString() == audience,
            JsonValueKind.Array => aud.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String && a.GetString() == audience),
            _ => false,
        };
    }

    private static JsonDocument? ParseObject(string part)
    {
        var bytes = DecodeBase64Url(part);
        if (bytes is null) return null;
        try
        {
            var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc;
            doc.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[]? DecodeBase64Url(string part)
    {
        if (part.Length == 0) return null;
        try
        {
            return Base64Url.DecodeFromChars(part);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static JwtVerifyResult Fail(string reason) =>
        new() { IsValid = false, Reason = reason, ClaimsJson = "" };
}
