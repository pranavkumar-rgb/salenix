using System.Text;

namespace Salenix.Crypto;

/// <summary>
/// Port of Postgres contrib/pg_trgm (trgm_op.c): similarity() and non-strict word_similarity().
/// Words = runs of letters/digits, lower-cased, padded "  word "; trigrams are 3-character windows.
/// Results are computed in float (float4) like Postgres.
/// </summary>
internal static class Trigram
{
    /// <summary>pg_trgm similarity: common / (n1 + n2 - common) over distinct trigrams.</summary>
    public static float Similarity(string a, string b)
    {
        var t1 = new HashSet<string>(Sequence(a), StringComparer.Ordinal);
        var t2 = new HashSet<string>(Sequence(b), StringComparer.Ordinal);
        if (t1.Count == 0 || t2.Count == 0) return 0f;
        var common = t1.Count(t2.Contains);
        return CalcSml(common, t1.Count, t2.Count);
    }

    /// <summary>pg_trgm word_similarity(needle, haystack): best match of the needle's trigram set
    /// against any continuous extent of the haystack's ordered trigrams (iterate_word_similarity, non-strict).</summary>
    public static float WordSimilarity(string needle, string haystack)
    {
        var t1 = new HashSet<string>(Sequence(needle), StringComparer.Ordinal);
        var t2 = Sequence(haystack);
        if (t1.Count == 0 || t2.Count == 0) return 0f;

        var all = t1.Union(t2).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < all.Count; i++) index[all[i]] = i;
        var found = all.Select(t1.Contains).ToArray();
        var ulen1 = t1.Count;
        var trg2 = t2.Select(t => index[t]).ToArray();

        var lastPos = Enumerable.Repeat(-1, all.Count).ToArray();
        int ulen2 = 0, count = 0, lower = -1;
        var smlrMax = 0f;

        for (var i = 0; i < trg2.Length; i++)
        {
            var trgIndex = trg2[i];
            if (lower >= 0 || found[trgIndex])
            {
                if (lastPos[trgIndex] < 0)
                {
                    ulen2++;
                    if (found[trgIndex]) count++;
                }
                lastPos[trgIndex] = i;
            }

            if (!found[trgIndex]) continue;

            var upper = i;
            if (lower == -1)
            {
                lower = i;
                ulen2 = 1;
            }
            var smlrCur = CalcSml(count, ulen1, ulen2);

            // Try to move the lower bound for a greater similarity.
            int tmpCount = count, tmpUlen2 = ulen2, prevLower = lower;
            for (var tmpLower = lower; tmpLower <= upper; tmpLower++)
            {
                var smlrTmp = CalcSml(tmpCount, ulen1, tmpUlen2);
                if (smlrTmp > smlrCur)
                {
                    smlrCur = smlrTmp;
                    ulen2 = tmpUlen2;
                    lower = tmpLower;
                    count = tmpCount;
                }
                var tmpTrgIndex = trg2[tmpLower];
                if (lastPos[tmpTrgIndex] == tmpLower)
                {
                    tmpUlen2--;
                    if (found[tmpTrgIndex]) tmpCount--;
                }
            }
            smlrMax = Math.Max(smlrMax, smlrCur);

            // Forget trigrams that are now before the lower bound.
            for (var tmpLower = prevLower; tmpLower < lower; tmpLower++)
            {
                var tmpTrgIndex = trg2[tmpLower];
                if (lastPos[tmpTrgIndex] == tmpLower) lastPos[tmpTrgIndex] = -1;
            }
        }
        return smlrMax;
    }

    /// <summary>float4 result as decimal with 8 places (0.36363637f -> 0.36363637).</summary>
    public static decimal ToDecimal(float value) => Math.Round((decimal)(double)value, 8);

    private static float CalcSml(int count, int len1, int len2) => (float)count / (float)(len1 + len2 - count);

    /// <summary>Ordered trigrams of all words (duplicates kept), as pg_trgm generate_trgm_only.</summary>
    internal static List<string> Sequence(string text)
    {
        var result = new List<string>();
        var word = new List<string>();
        foreach (var rune in (text ?? "").EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                word.Add(Rune.ToLowerInvariant(rune).ToString());
                continue;
            }
            AddWord(word, result);
        }
        AddWord(word, result);
        return result;
    }

    private static void AddWord(List<string> word, List<string> result)
    {
        if (word.Count == 0) return;
        var padded = new List<string>(word.Count + 3) { " ", " " };
        padded.AddRange(word);
        padded.Add(" ");
        for (var i = 0; i + 2 < padded.Count; i++)
            result.Add(padded[i] + padded[i + 1] + padded[i + 2]);
        word.Clear();
    }
}
