using Salenix.Crypto;

namespace Salenix_Crypto.Tests;

public class TimeZoneTests
{
    private static DateTime Utc(int y, int mo, int d, int h, int mi, int s = 0) => new(y, mo, d, h, mi, s, DateTimeKind.Utc);

    [Fact]
    public void Kolkata_is_plus_0530_and_crosses_midnight()
    {
        Assert.Equal(new DateTime(2026, 11, 11, 1, 30, 0), TimeZones.ConvertUtcToZone(Utc(2026, 11, 10, 20, 0), "Asia/Kolkata"));
    }

    [Fact]
    public void Utc_zone_returns_same_wall_clock()
    {
        Assert.Equal(new DateTime(2026, 11, 11, 5, 0, 0), TimeZones.ConvertUtcToZone(Utc(2026, 11, 11, 5, 0), "UTC"));
    }

    [Fact]
    public void New_York_dst_start_2026_03_08()
    {
        Assert.Equal(new DateTime(2026, 3, 8, 1, 30, 0), TimeZones.ConvertUtcToZone(Utc(2026, 3, 8, 6, 30), "America/New_York"));
        Assert.Equal(new DateTime(2026, 3, 8, 3, 30, 0), TimeZones.ConvertUtcToZone(Utc(2026, 3, 8, 7, 30), "America/New_York"));
    }

    [Fact]
    public void Unspecified_kind_is_treated_as_utc()
    {
        var unspecified = new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(TimeZones.ConvertUtcToZone(Utc(2026, 11, 10, 20, 0), "Asia/Kolkata"), TimeZones.ConvertUtcToZone(unspecified, "Asia/Kolkata"));
    }

    [Fact]
    public void Result_kind_is_unspecified()
    {
        Assert.Equal(DateTimeKind.Unspecified, TimeZones.ConvertUtcToZone(Utc(2026, 11, 10, 20, 0), "Asia/Kolkata").Kind);
    }

    [Fact]
    public void TodayIn_Kolkata_changes_at_1830_utc()
    {
        Assert.Equal(new DateTime(2026, 11, 10), TimeZones.TodayIn("Asia/Kolkata", Utc(2026, 11, 10, 18, 29, 59)));
        Assert.Equal(new DateTime(2026, 11, 11), TimeZones.TodayIn("Asia/Kolkata", Utc(2026, 11, 10, 18, 30, 0)));
    }

    [Theory]
    [InlineData("Mars/Base")]
    [InlineData("")]
    [InlineData("India Standard Time")]
    public void Unknown_zone_raises_TIMEZONE_UNKNOWN(string zone)
    {
        var ex = Assert.Throws<SalenixCryptoException>(() => TimeZones.ConvertUtcToZone(Utc(2026, 11, 11, 0, 0), zone));
        Assert.StartsWith("TIMEZONE_UNKNOWN: ", ex.Message);
    }

    [Fact]
    public void Public_TodayIn_returns_a_date_without_time()
    {
        Assert.Equal(TimeSpan.Zero, new SalenixCrypto().TodayIn("Asia/Kolkata").TimeOfDay);
    }
}
