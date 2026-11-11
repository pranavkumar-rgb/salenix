using Salenix.Crypto;

namespace Salenix_Crypto.Tests;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("Acmé Pvt. Ltd.", "acme")]
    [InlineData("Ltd", "ltd")]
    [InlineData("  Tata   Steel Limited ", "tata steel")]
    [InlineData("Larsen & Toubro Ltd", "larsen toubro")]
    [InlineData("Société Générale", "societe generale")]
    [InlineData("", "")]
    [InlineData("!!!", "")]
    public void CompanyName_cases(string input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.CompanyName(input));
    }

    [Theory]
    [InlineData("José", "O'Neil", "jose o neil")]
    [InlineData("  Anil ", "", "anil")]
    [InlineData("", "", "")]
    public void ContactName_cases(string first, string last, string expected)
    {
        Assert.Equal(expected, NameNormalizer.ContactName(first, last));
    }
}
