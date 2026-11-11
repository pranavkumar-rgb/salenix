using OutSystems.ExternalLibraries.SDK;

namespace Salenix.Crypto;

/// <summary>
/// Salenix_Crypto: timezone, token, hash, HMAC, pg_trgm similarity, name
/// normalization and HS256 verification helpers for the Salenix ODC apps.
/// Errors are raised with a fixed code prefix: CRYPTO_ERROR: / TIMEZONE_UNKNOWN:.
/// </summary>
[OSInterface(Name = "Salenix_Crypto", Description = "Timezone, token, hash, HMAC, trigram similarity, name normalization and HS256 verification helpers for Salenix.")]
public interface ISalenixCrypto
{
    [OSAction(Description = "Converts a UTC date time to the wall-clock time of an IANA zone (e.g. Asia/Kolkata). Raises TIMEZONE_UNKNOWN: for unknown zones.", ReturnName = "LocalDateTime")]
    DateTime ConvertUtcToZone(
        [OSParameter(Description = "Date time in UTC.")] DateTime UtcDateTime,
        [OSParameter(Description = "IANA time zone name, e.g. Asia/Kolkata.")] string IanaZone);

    [OSAction(Description = "Today's date in an IANA zone. Raises TIMEZONE_UNKNOWN: for unknown zones.", ReturnName = "Today", ReturnType = OSDataType.Date)]
    DateTime TodayIn(
        [OSParameter(Description = "IANA time zone name, e.g. Asia/Kolkata.")] string IanaZone);

    [OSAction(Description = "Cryptographically random URL-safe token (base64url, no padding). 32 bytes give 43 characters.", ReturnName = "Token")]
    string NewToken(
        [OSParameter(Description = "Number of random bytes, 16 to 64.")] int Bytes = 32);

    [OSAction(Description = "SHA-256 of the UTF-8 text, lower-case hex.", ReturnName = "Hash")]
    string Sha256Hex(
        [OSParameter(Description = "Text to hash.")] string Value);

    [OSAction(Description = "SHA-256 of the UTF-8 text, base64url without padding (PKCE S256 challenge).", ReturnName = "Hash")]
    string Sha256Base64Url(
        [OSParameter(Description = "Text to hash.")] string Value);

    [OSAction(Description = "HMAC-SHA256 with a UTF-8 text key, lower-case hex.", ReturnName = "Signature")]
    string HmacSha256Hex(
        [OSParameter(Description = "Secret key (UTF-8 text).")] string Key,
        [OSParameter(Description = "Message to sign.")] string Message);

    [OSAction(Description = "Compares two texts in constant time. Different lengths return False at once.", ReturnName = "IsEqual")]
    bool ConstantTimeEquals(
        [OSParameter(Description = "First text.")] string A,
        [OSParameter(Description = "Second text.")] string B);

    [OSAction(Description = "Trigram similarity, same algorithm as Postgres pg_trgm similarity(a, b). Returns 0..1.", ReturnName = "Similarity")]
    decimal TrigramSimilarity(
        [OSParameter(Description = "First text.")] string A,
        [OSParameter(Description = "Second text.")] string B);

    [OSAction(Description = "Word similarity, same algorithm as Postgres pg_trgm word_similarity(needle, haystack). Returns 0..1.", ReturnName = "Similarity")]
    decimal WordSimilarity(
        [OSParameter(Description = "Text searched for (typed name).")] string Needle,
        [OSParameter(Description = "Text searched in (stored name).")] string Haystack);

    [OSAction(Description = "Normalized company name for dedup (accents, punctuation and legal suffixes removed), same as the source normalizeCompanyName.", ReturnName = "Normalized")]
    string NormalizeCompanyName(
        [OSParameter(Description = "Company name as typed.")] string Name);

    [OSAction(Description = "Normalized contact full name for dedup, same as the source normalizeContactName.", ReturnName = "Normalized")]
    string NormalizeContactName(
        [OSParameter(Description = "First name.")] string FirstName,
        [OSParameter(Description = "Last name.")] string LastName);

    [OSAction(Description = "Verifies an HS256 token (signature, issuer, audience, exp, iat with leeway). Business claims (use, jti, bh, sub, tid) are checked by the caller from ClaimsJson.", ReturnName = "Result")]
    JwtVerifyResult JwtVerifyHs256(
        [OSParameter(Description = "The compact token header.payload.signature.")] string Token,
        [OSParameter(Description = "HMAC secret (UTF-8 text).")] string Secret,
        [OSParameter(Description = "Expected iss claim.")] string Issuer,
        [OSParameter(Description = "Expected aud claim (string or member of the aud array).")] string Audience,
        [OSParameter(Description = "Allowed clock skew in seconds.")] int LeewaySeconds = 10);

    [OSAction(Description = "Payload JSON of a token WITHOUT verifying it (only to choose the secret). Empty text if malformed.", ReturnName = "ClaimsJson")]
    string JwtPeekClaims(
        [OSParameter(Description = "The compact token.")] string Token);
}

[OSStructure(Description = "Result of JwtVerifyHs256.")]
public struct JwtVerifyResult
{
    [OSStructureField(Description = "True when signature and standard claims are valid.", IsMandatory = true)]
    public bool IsValid;

    [OSStructureField(Description = "Empty when valid, else malformed, bad_alg, bad_signature, bad_issuer, bad_audience, expired, not_yet_valid.", Length = 50)]
    public string Reason;

    [OSStructureField(Description = "Payload JSON when valid, else empty.", Length = 4000)]
    public string ClaimsJson;
}
