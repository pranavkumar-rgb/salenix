using Salenix.Crypto;

namespace Salenix_Crypto.Tests;

public class HashingTests
{
    [Fact]
    public void NewToken_32_bytes_is_43_url_safe_chars()
    {
        var token = Hashing.NewToken(32);
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void NewToken_values_are_unique()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => Hashing.NewToken(32)).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(65)]
    public void NewToken_out_of_range_raises_CRYPTO_ERROR(int bytes)
    {
        var ex = Assert.Throws<SalenixCryptoException>(() => Hashing.NewToken(bytes));
        Assert.StartsWith("CRYPTO_ERROR: ", ex.Message);
    }

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("₹", "d8c3e87cd7f5b7d388f9dc1e35ccee09640ef3dca63d449d9ef59fc323a87a20")] // Node createHash('sha256')
    public void Sha256Hex_known_vectors(string value, string expected)
    {
        Assert.Equal(expected, Hashing.Sha256Hex(value));
    }

    [Fact]
    public void Sha256Base64Url_RFC7636_PKCE_vector()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", Hashing.Sha256Base64Url("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void HmacSha256Hex_RFC4231_case_2()
    {
        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", Hashing.HmacSha256Hex("Jefe", "what do ya want for nothing?"));
    }

    [Fact]
    public void HmacSha256Hex_matches_source_askmira_derive()
    {
        // Node: createHmac('sha256', serverKey).update(`askmira-saleinx:v1:${tenantId}:${nonce}`).digest('hex')
        const string serverKey = "test-server-key-0123456789abcdef0123456789";
        const string message = "askmira-saleinx:v1:3f2b6c1e-8d4a-4b7f-9c2e-1a5d7e9f0b3c:n0nce-AbC_123";
        Assert.Equal("273f773c5edca02d77d8c0c7c58376c013451e7c29fb9459789f143083796754", Hashing.HmacSha256Hex(serverKey, message));
    }

    [Fact]
    public void HmacSha256Hex_empty_key_raises_CRYPTO_ERROR()
    {
        var ex = Assert.Throws<SalenixCryptoException>(() => Hashing.HmacSha256Hex("", "x"));
        Assert.StartsWith("CRYPTO_ERROR: ", ex.Message);
    }

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "abcd", false)]
    [InlineData("", "", true)]
    public void ConstantTimeEquals_cases(string a, string b, bool expected)
    {
        Assert.Equal(expected, Hashing.ConstantTimeEquals(a, b));
    }
}
