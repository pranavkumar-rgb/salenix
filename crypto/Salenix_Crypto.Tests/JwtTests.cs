using System.Buffers.Text;
using System.Text;
using Salenix.Crypto;

namespace Salenix_Crypto.Tests;

public class JwtTests
{
    private const string Secret = "workspace-secret-0123456789abcdef";
    private const string Issuer = "askmira";
    private const string Audience = "askmira:saleinx";
    private const long Now = 1793000030;

    private static string B64(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

    private static string Sign(string headerJson, string payloadJson, string secret = Secret)
    {
        var signingInput = $"{B64(headerJson)}.{B64(payloadJson)}";
        return $"{signingInput}.{Base64Url.EncodeToString(Hashing.HmacSha256(secret, signingInput))}";
    }

    private static string Payload(string extra = "", long exp = Now + 60, string aud = "\"askmira:saleinx\"", string iss = "askmira") =>
        $"{{\"iss\":\"{iss}\",\"aud\":{aud},\"sub\":\"s-1\",\"exp\":{exp}{extra}}}";

    private const string Header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";

    private static JwtVerifyResult Verify(string token, string secret = Secret) =>
        JwtHs256.Verify(token, secret, Issuer, Audience, 10, Now);

    [Fact]
    public void Valid_token_returns_claims()
    {
        var result = Verify(Sign(Header, Payload()));
        Assert.True(result.IsValid);
        Assert.Equal("", result.Reason);
        Assert.Contains("\"sub\":\"s-1\"", result.ClaimsJson);
    }

    [Fact]
    public void Token_made_by_node_source_style_verifies()
    {
        // Node: createHmac('sha256', derived).update(`${h}.${p}`).digest('base64url'); derived = askmira derive() hex.
        const string derived = "273f773c5edca02d77d8c0c7c58376c013451e7c29fb9459789f143083796754";
        const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJhc2ttaXJhIiwiYXVkIjoiYXNrbWlyYTpzYWxlaW54Iiwic3ViIjoiOWExYzJkM2UtMDAwMC00MDAwLTgwMDAtMDAwMDAwMDAwMDAxIiwidXNlIjoicXVlcnkiLCJqdGkiOiJqdGktMSIsImJoIjoiYzc2NzcwNGMyNmI0YjNkZGNmMjEzYmM1ZGM4ZmM3MWNhNTE3M2Q4ZWJhZGQxYmY4ZjMyZDc3ZTkwZjk3NmI2NCIsImlhdCI6MTc5MzAwMDAwMCwiZXhwIjoxNzkzMDAwMDYwfQ.yfoTrsOGawhOnex7hGwluaZzj9x2NYSoB8zFDG9P8Ys";
        var result = JwtHs256.Verify(token, derived, Issuer, Audience, 10, Now);
        Assert.True(result.IsValid);
        Assert.Contains("\"use\":\"query\"", result.ClaimsJson);
        Assert.Contains("\"jti\":\"jti-1\"", result.ClaimsJson);
    }

    [Fact]
    public void Changed_payload_is_bad_signature()
    {
        var parts = Sign(Header, Payload()).Split('.');
        var tampered = $"{parts[0]}.{B64(Payload(extra: ",\"x\":1"))}.{parts[2]}";
        Assert.Equal("bad_signature", Verify(tampered).Reason);
    }

    [Fact]
    public void Wrong_secret_is_bad_signature()
    {
        Assert.Equal("bad_signature", Verify(Sign(Header, Payload()), "another-secret-0123456789abcdef").Reason);
    }

    [Fact]
    public void Alg_none_is_bad_alg()
    {
        Assert.Equal("bad_alg", Verify(Sign("{\"alg\":\"none\"}", Payload())).Reason);
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("")]
    [InlineData("a.b.c.d")]
    [InlineData("!!.!!.!!")]
    public void Malformed_tokens(string token)
    {
        var result = Verify(token);
        Assert.False(result.IsValid);
        Assert.Equal("malformed", result.Reason);
    }

    [Fact]
    public void Expired_beyond_leeway()
    {
        Assert.Equal("expired", Verify(Sign(Header, Payload(exp: Now - 11))).Reason);
    }

    [Fact]
    public void Expired_within_leeway_is_valid()
    {
        Assert.True(Verify(Sign(Header, Payload(exp: Now - 9))).IsValid);
    }

    [Fact]
    public void Missing_exp_is_expired()
    {
        Assert.Equal("expired", Verify(Sign(Header, "{\"iss\":\"askmira\",\"aud\":\"askmira:saleinx\"}")).Reason);
    }

    [Fact]
    public void Iat_in_the_future_is_not_yet_valid()
    {
        Assert.Equal("not_yet_valid", Verify(Sign(Header, Payload(extra: $",\"iat\":{Now + 11}"))).Reason);
    }

    [Fact]
    public void Audience_array_containing_audience_is_valid()
    {
        Assert.True(Verify(Sign(Header, Payload(aud: "[\"other\",\"askmira:saleinx\"]"))).IsValid);
    }

    [Fact]
    public void Wrong_audience_is_bad_audience()
    {
        Assert.Equal("bad_audience", Verify(Sign(Header, Payload(aud: "\"other\""))).Reason);
    }

    [Fact]
    public void Wrong_issuer_is_bad_issuer()
    {
        Assert.Equal("bad_issuer", Verify(Sign(Header, Payload(iss: "someone"))).Reason);
    }

    [Fact]
    public void Empty_secret_raises_CRYPTO_ERROR()
    {
        var ex = Assert.Throws<SalenixCryptoException>(() => Verify(Sign(Header, Payload()), ""));
        Assert.StartsWith("CRYPTO_ERROR: ", ex.Message);
    }

    [Fact]
    public void Peek_returns_payload_without_checking()
    {
        var token = Sign(Header, Payload(), "any-other-secret");
        Assert.Contains("\"sub\":\"s-1\"", JwtHs256.Peek(token));
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("x.!!.y")]
    public void Peek_malformed_is_empty(string token)
    {
        Assert.Equal("", JwtHs256.Peek(token));
    }
}
