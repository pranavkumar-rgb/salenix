namespace Salenix.Crypto;

/// <summary>ODC entry point. Stateless: every call delegates to the static helpers.</summary>
public class SalenixCrypto : ISalenixCrypto
{
    public DateTime ConvertUtcToZone(DateTime UtcDateTime, string IanaZone) =>
        TimeZones.ConvertUtcToZone(UtcDateTime, IanaZone);

    public DateTime TodayIn(string IanaZone) =>
        TimeZones.TodayIn(IanaZone, DateTime.UtcNow);

    public string NewToken(int Bytes = 32) => Hashing.NewToken(Bytes);

    public string Sha256Hex(string Value) => Hashing.Sha256Hex(Value);

    public string Sha256Base64Url(string Value) => Hashing.Sha256Base64Url(Value);

    public string HmacSha256Hex(string Key, string Message) => Hashing.HmacSha256Hex(Key, Message);

    public bool ConstantTimeEquals(string A, string B) => Hashing.ConstantTimeEquals(A, B);

    public decimal TrigramSimilarity(string A, string B) => Trigram.ToDecimal(Trigram.Similarity(A, B));

    public decimal WordSimilarity(string Needle, string Haystack) =>
        Trigram.ToDecimal(Trigram.WordSimilarity(Needle, Haystack));

    public string NormalizeCompanyName(string Name) => NameNormalizer.CompanyName(Name);

    public string NormalizeContactName(string FirstName, string LastName) =>
        NameNormalizer.ContactName(FirstName, LastName);

    public JwtVerifyResult JwtVerifyHs256(string Token, string Secret, string Issuer, string Audience, int LeewaySeconds = 10) =>
        JwtHs256.Verify(Token, Secret, Issuer, Audience, LeewaySeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public string JwtPeekClaims(string Token) => JwtHs256.Peek(Token);
}

/// <summary>Raised for invalid input; the message starts with a fixed code (CRYPTO_ERROR: / TIMEZONE_UNKNOWN:).</summary>
public class SalenixCryptoException(string message) : Exception(message);
