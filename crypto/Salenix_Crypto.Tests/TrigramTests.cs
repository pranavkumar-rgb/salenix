using Salenix.Crypto;

namespace Salenix_Crypto.Tests;

public class TrigramTests
{
    // Expected values: computed 2026-11-11 with a line-by-line port of pg_trgm trgm_op.c (no Postgres available).
    // Anchor from the PostgreSQL pg_trgm docs: word_similarity('word', 'two words') = 0.8.
    // TODO: replace with real values from Postgres when available:
    //   CREATE EXTENSION pg_trgm; SELECT a, b, similarity(a, b), word_similarity(a, b) FROM (VALUES ...) v(a, b);
    public static TheoryData<string, string, double, double> Pairs => new()
    {
        { "word", "two words", 0.36363637, 0.8 },
        { "acme", "acme", 1, 1 },
        { "acme", "acme corp", 0.5, 1 },
        { "tata steel", "tata steels", 0.76923078, 0.90909094 },
        { "infosys", "infosis", 0.45454547, 0.625 },
        { "reliance industries", "reliance industry", 0.72727275, 0.8 },
        { "hdfc bank", "icici bank", 0.33333334, 0.5 },
        { "abc", "xyz", 0, 0 },
        { "larsen toubro", "larsen and toubro", 0.77777779, 0.77777779 },
        { "Wipro-Tech", "wipro tech", 1, 1 },
        { "", "", 0, 0 },
        { "ab", "abc", 0.4, 0.66666669 },
    };

    private const double Tolerance = 0.000001;

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Similarity_matches_pg_trgm(string a, string b, double similarity, double _)
    {
        Assert.Equal(similarity, (double)new SalenixCrypto().TrigramSimilarity(a, b), Tolerance);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void WordSimilarity_matches_pg_trgm(string a, string b, double _, double wordSimilarity)
    {
        Assert.Equal(wordSimilarity, (double)new SalenixCrypto().WordSimilarity(a, b), Tolerance);
    }

    [Fact]
    public void Trigram_sequence_matches_pg_docs_example()
    {
        Assert.Equal(new[] { "  w", " wo", "wor", "ord", "rd " }, Trigram.Sequence("word"));
        Assert.Equal(new[] { "  t", " tw", "two", "wo ", "  w", " wo", "wor", "ord", "rds", "ds " }, Trigram.Sequence("two words"));
    }

    [Fact]
    public void Similarity_is_symmetric()
    {
        Assert.Equal(Trigram.Similarity("infosys", "infosis"), Trigram.Similarity("infosis", "infosys"));
    }

    [Fact]
    public void Infosis_is_a_duplicate_only_through_word_similarity()
    {
        var crypto = new SalenixCrypto();
        var score = Math.Max(crypto.TrigramSimilarity("infosys", "infosis"), crypto.WordSimilarity("infosis", "infosys"));
        Assert.True(crypto.TrigramSimilarity("infosys", "infosis") < 0.55m);
        Assert.True(score >= 0.55m);
    }

    [Fact]
    public void Result_has_at_most_8_decimals()
    {
        var value = new SalenixCrypto().TrigramSimilarity("word", "two words");
        Assert.Equal(0.36363637m, value);
    }
}
