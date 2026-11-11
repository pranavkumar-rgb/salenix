using System.Text;
using System.Text.RegularExpressions;

namespace Salenix.Crypto;

/// <summary>1:1 port of source/src/lib/dedup/normalize.ts (normalizeCompanyName, normalizeContactName).</summary>
internal static partial class NameNormalizer
{
    private static readonly HashSet<string> LegalSuffixTokens = new(StringComparer.Ordinal)
    {
        "ltd", "limited", "pvt", "private", "llp", "inc", "incorporated", "corp", "corporation",
        "co", "company", "plc", "llc", "gmbh", "sarl", "bv", "ag",
    };

    public static string CompanyName(string name)
    {
        var cleaned = Clean(name);
        if (cleaned.Length == 0) return "";
        var tokens = cleaned.Split(' ').Where(t => t.Length > 0 && !LegalSuffixTokens.Contains(t));
        var normalized = string.Join(' ', tokens).Trim();
        return normalized.Length > 0 ? normalized : cleaned;
    }

    public static string ContactName(string firstName, string lastName) =>
        Clean($"{firstName ?? ""} {lastName ?? ""}");

    /// <summary>NFKD, drop U+0300–U+036F, lower case, non [a-z0-9\s] to space, collapse whitespace, trim.</summary>
    private static string Clean(string value)
    {
        var decomposed = (value ?? "").Normalize(NormalizationForm.FormKD);
        var noMarks = Diacritics().Replace(decomposed, "");
        var lower = noMarks.ToLowerInvariant();
        var ascii = NonAlnumOrSpace().Replace(lower, " ");
        return Whitespace().Replace(ascii, " ").Trim();
    }

    [GeneratedRegex("[̀-ͯ]")]
    private static partial Regex Diacritics();

    [GeneratedRegex(@"[^a-z0-9\s]")]
    private static partial Regex NonAlnumOrSpace();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
